using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MvcMusic.Data;
using MvcMusic.Models;
using MvcMusic.Utils;
using MvcMusic.ViewModels;

namespace MvcMusic.Controllers
{
    [Authorize]
    public class OrdersController : Controller
    {
        private readonly MvcMusicContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IActivityLogService _activityLogger;

        public OrdersController(MvcMusicContext context, UserManager<ApplicationUser> userManager, IActivityLogService activityLogger)
        {
            _context = context;
            _userManager = userManager;
            _activityLogger = activityLogger;
        }

        private async Task<(string? id, string? name, string? role, string? fullName)> CurrentEmployeeInfoAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return (null, null, null, null);
            var roles = await _userManager.GetRolesAsync(user);
            return (user.Id, user.UserName, roles.FirstOrDefault(), user.FullName);
        }

        // GET: /Orders
        [Authorize(Roles = "Admin,SuperAdmin,SalesStaff")]
        public async Task<IActionResult> Index(int page = 1, int pageSize = 15)
        {
            var totalCount = await _context.Order.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            page = Math.Max(1, Math.Min(totalPages, page));

            // Fetch paged orders + customers in one JOIN query
            var ordersQuery = await _context.Order
                .OrderByDescending(o => o.OrderDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Join(_context.Users,
                    o => o.CustomerId,
                    u => u.Id,
                    (o, u) => new { o.Id, o.TotalAmount, o.Status, o.OrderDate, CustomerName = u.FirstName + " " + u.LastName })
                .ToListAsync();

            // Batch-fetch only items for the current page
            var orderIds = ordersQuery.Select(o => o.Id).ToList();
            var allItems = await _context.OrderItem
                .Where(oi => orderIds.Contains(oi.OrderId))
                .Select(oi => new { oi.OrderId, oi.Quantity, ProductName = oi.Product != null ? oi.Product.Name : "—" })
                .ToListAsync();

            var itemsByOrder = allItems.GroupBy(i => i.OrderId).ToDictionary(g => g.Key, g => g.ToList());

            var orders = ordersQuery.Select(o => new RecentOrderItem
            {
                Id = o.Id,
                CustomerName = o.CustomerName,
                Products = itemsByOrder.TryGetValue(o.Id, out var items) ? string.Join(", ", items.Select(i => i.ProductName).Distinct()) : "—",
                TotalQuantity = itemsByOrder.TryGetValue(o.Id, out var qItems) ? qItems.Sum(i => i.Quantity) : 0,
                Amount = o.TotalAmount,
                Status = o.Status,
                Date = o.OrderDate
            }).ToList();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.PageSize = pageSize;

            return View(orders);
        }

        // GET: /Orders/History
        public async Task<IActionResult> History(string? status = "All")
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var query = _context.Order
                .Where(o => o.CustomerId == user.Id)
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .ThenInclude(p => p!.ProductImages)
                .OrderByDescending(o => o.OrderDate)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status) && status != "All")
            {
                query = query.Where(o => o.Status == status);
            }

            var orders = await query.ToListAsync();
            ViewBag.CurrentStatus = status;

            return View(orders);
        }

        // GET: /Orders/Detail/5
        public async Task<IActionResult> Detail(int id)
        {
            var order = await _context.Order
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .ThenInclude(p => p!.ProductImages)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null) return NotFound();

            // Security check for regular users
            var user = await _userManager.GetUserAsync(User);
            bool isAdmin = User.IsInRole("Admin") || User.IsInRole("SuperAdmin") || User.IsInRole("SalesStaff");
            if (!isAdmin && order.CustomerId != user?.Id) return Forbid();

            var customer = await _context.Users.FindAsync(order.CustomerId);
            ViewBag.CustomerName = $"{customer?.FirstName} {customer?.LastName}";
            ViewBag.CustomerEmail = customer?.Email;

            return View(order);
        }

        // GET: /Orders/GetTableData
        [HttpGet]
        [Authorize(Roles = "Admin,SuperAdmin,SalesStaff")]
        public async Task<IActionResult> GetTableData(string? status = null, string? includeIds = null)
        {
            var ordersQuery = await _context.Order
                .OrderByDescending(o => o.OrderDate)
                .Join(_context.Users,
                    o => o.CustomerId,
                    u => u.Id,
                    (o, u) => new { o.Id, o.TotalAmount, o.Status, o.OrderDate, CustomerName = u.FirstName + " " + u.LastName })
                .ToListAsync();

            if (!string.IsNullOrEmpty(includeIds))
            {
                var extraIds = includeIds.Split(',').Select(s => int.TryParse(s, out int id) ? id : 0).Where(id => id > 0).ToList();
                foreach (var id in extraIds)
                {
                    if (!ordersQuery.Any(o => o.Id == id))
                    {
                        var o = await _context.Order.FindAsync(id);
                        if (o != null)
                        {
                            var u = await _context.Users.FindAsync(o.CustomerId);
                            ordersQuery.Add(new { o.Id, o.TotalAmount, o.Status, o.OrderDate, CustomerName = (u?.FirstName + " " + u?.LastName) ?? "Unknown" });
                        }
                    }
                }
            }

            var orderIds = ordersQuery.Select(o => o.Id).ToList();
            var allItems = await _context.OrderItem
                .Where(oi => orderIds.Contains(oi.OrderId))
                .Select(oi => new { oi.OrderId, ProductName = oi.Product != null ? oi.Product.Name : (oi.ProductName ?? "—") })
                .ToListAsync();

            var itemsByOrder = allItems.GroupBy(i => i.OrderId).ToDictionary(g => g.Key, g => string.Join(", ", g.Select(i => i.ProductName).Distinct()));

            var rows = ordersQuery.Select(o => new
            {
                id = o.Id,
                customer = o.CustomerName,
                products = itemsByOrder.GetValueOrDefault(o.Id, "—"),
                amount = o.TotalAmount,
                status = o.Status,
                date = o.OrderDate.ToString("yyyy-MM-dd HH:mm:ss"),
                _isExtra = !string.IsNullOrEmpty(status) && status != "all" && 
                          o.Status != status && 
                          !string.IsNullOrEmpty(includeIds) && includeIds.Split(',').Contains(o.Id.ToString())
            }).ToList();

            var columns = new List<object>
            {
                new { id = "id", updatable = false },
                new { id = "customer", updatable = false },
                new { id = "products", updatable = false },
                new { id = "amount", updatable = false },
                new { 
                    id = "status", 
                    updatable = true, 
                    type = "select", 
                    options = new[] { 
                        new { value = "Pending", label = "Pending" },
                        new { value = "OnDelivery", label = "On Delivery" },
                        new { value = "Delivered", label = "Delivered" },
                        new { value = "Cancelled", label = "Cancelled" }
                    }
                },
                new { id = "date", updatable = false },
                new { id = "actions", updatable = false }
            };

            return Json(new { columns, rows, updateRequest = Url.Action("UpdateOrders") });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,SuperAdmin,SalesStaff")]
        public async Task<IActionResult> UpdateOrders([FromBody] List<OrderUpdateModel> changes)
        {
            if (changes == null || !changes.Any()) return Json(new { success = true });

            var (cId, cName, cRole, cFull) = await CurrentEmployeeInfoAsync();

            foreach (var change in changes)
            {
                var order = await _context.Order.Include(o => o.OrderItems).FirstOrDefaultAsync(o => o.Id == change.Id);
                if (order == null) continue;

                if (change.Field == "status" && order.Status != change.Value)
                {
                    var oldStatus = order.Status;
                    order.Status = change.Value!;

                    if (order.Status == "Delivered")
                    {
                        await ProcessDeliveryLogicAsync(order);
                        await _activityLogger.LogAsync(ActivityAction.DeliverOrder, $"Updated status of order <a href='/orders/detail/{order.Id}' class='order-link'>#{order.Id}</a> from '{oldStatus}' to 'Delivered'.", cId, cName, cRole, cFull);
                    }
                    else if (order.Status == "Cancelled")
                    {
                        await _activityLogger.LogAsync(ActivityAction.CancelOrder, $"Updated status of order <a href='/orders/detail/{order.Id}' class='order-link'>#{order.Id}</a> from '{oldStatus}' to 'Cancelled'.", cId, cName, cRole, cFull);
                    }
                    else
                    {
                        await _activityLogger.LogAsync(ActivityAction.UpdateProduct, $"Updated status of order <a href='/orders/detail/{order.Id}' class='order-link'>#{order.Id}</a> from '{oldStatus}' to '{order.Status}'.", cId, cName, cRole, cFull);
                    }
                }
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        private async Task ProcessDeliveryLogicAsync(Order order)
        {
            foreach (var item in order.OrderItems)
            {
                var product = await _context.Product.FindAsync(item.ProductId);
                if (product != null)
                {
                    product.SoldAmount += item.Quantity;
                    product.RatingsCount++;
                    if (product.Rating < 4.9) product.Rating = 4.9;
                    else if (product.Rating < 5.0) product.Rating = Math.Min(5.0, product.Rating + 0.01);
                }
            }
        }

        // POST: /Orders/MarkDelivered/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,SuperAdmin,SalesStaff")]
        public async Task<IActionResult> MarkDelivered(int id)
        {
            var order = await _context.Order.Include(o => o.OrderItems).FirstOrDefaultAsync(o => o.Id == id);
            if (order == null) return NotFound();

            if (order.Status != "Delivered")
            {
                order.Status = "Delivered";
                await ProcessDeliveryLogicAsync(order);
                await _context.SaveChangesAsync();

                var (cId, cName, cRole, cFull) = await CurrentEmployeeInfoAsync();
                await _activityLogger.LogAsync(ActivityAction.DeliverOrder, $"Marked order <a href='/orders/detail/{id}' class='order-link'>#{id}</a> as Delivered.", cId, cName, cRole, cFull);

                TempData["Success"] = $"Order #{id} marked as Delivered.";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: /Orders/CancelOrder/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,SuperAdmin,SalesStaff")]
        public async Task<IActionResult> CancelOrder(int id)
        {
            var order = await _context.Order.FindAsync(id);
            if (order == null) return NotFound();

            if (order.Status != "Cancelled")
            {
                order.Status = "Cancelled";
                await _context.SaveChangesAsync();

                var (cId, cName, cRole, cFull) = await CurrentEmployeeInfoAsync();
                await _activityLogger.LogAsync(ActivityAction.CancelOrder, $"Cancelled order <a href='/orders/detail/{id}' class='order-link'>#{id}</a>.", cId, cName, cRole, cFull);

                TempData["Success"] = $"Order #{id} has been cancelled.";
            }

            return RedirectToAction(nameof(Index));
        }

        public class OrderUpdateModel { public int Id { get; set; } public string? Field { get; set; } public string? Value { get; set; } }
    }
}
