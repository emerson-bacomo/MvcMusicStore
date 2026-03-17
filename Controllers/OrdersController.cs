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
    [Authorize(Roles = "Admin,SuperAdmin,Staff")]
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

        // GET: /Orders/Detail/5
        public async Task<IActionResult> Detail(int id)
        {
            var order = await _context.Order
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .ThenInclude(p => p!.ProductImages)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null) return NotFound();

            var customer = await _context.Users.FindAsync(order.CustomerId);
            ViewBag.CustomerName = $"{customer?.FirstName} {customer?.LastName}";
            ViewBag.CustomerEmail = customer?.Email;

            return View(order);
        }

        // POST: /Orders/MarkDelivered/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkDelivered(int id)
        {
            var order = await _context.Order
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null) return NotFound();

            if (order.Status != "Delivered")
            {
                order.Status = "Delivered";

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

                await _context.SaveChangesAsync();

                var (cId, cName, cRole, cFull) = await CurrentEmployeeInfoAsync();
                await _activityLogger.LogAsync(ActivityAction.DeliverOrder, $"Marked order <a href='/orders/detail/{id}' class='order-link'>#{id}</a> as Delivered.", cId, cName, cRole, cFull);

                TempData["Success"] = $"Order #{id} marked as Delivered and sales updated.";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: /Orders/CancelOrder/5
        [HttpPost]
        [ValidateAntiForgeryToken]
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
    }
}
