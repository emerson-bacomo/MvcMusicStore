using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MvcMusic.Data;
using MvcMusic.Models;
using MvcMusic.Utils;

namespace MvcMusic.Controllers
{
    [Authorize(Roles = "Admin,SuperAdmin,CustomerStaff")]
    public class CustomersController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly MvcMusicContext _context;
        private readonly IActivityLogService _logger;

        public CustomersController(UserManager<ApplicationUser> userManager, MvcMusicContext context, IActivityLogService logger)
        {
            _userManager = userManager;
            _context = context;
            _logger = logger;
        }

        private async Task<(string? id, string? name, string? role, string? fullName)> CurrentEmployeeInfoAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return (null, null, null, null);
            var roles = await _userManager.GetRolesAsync(user);
            return (user.Id, user.UserName, roles.FirstOrDefault(), user.FullName);
        }

        // GET: /customers
        public async Task<IActionResult> Index()
        {
            var customers = await _userManager.GetUsersInRoleAsync("User");
            return View(customers.OrderByDescending(c => c.DateCreated).ToList());
        }

        // GET: /customers/profile/{id}
        public async Task<IActionResult> Profile(string id)
        {
            var customer = await _userManager.FindByIdAsync(id);
            if (customer == null) return NotFound();

            var orders = await _context.Order
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .Where(o => o.CustomerId == id)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            ViewBag.Customer = customer;
            return View(orders);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleBan(string id)
        {
            var customer = await _userManager.FindByIdAsync(id);
            if (customer == null) return NotFound();

            customer.IsBanned = !customer.IsBanned;
            await _userManager.UpdateAsync(customer);

            var (cId, cName, cRole, cFull) = await CurrentEmployeeInfoAsync();
            var action = customer.IsBanned ? ActivityAction.BanCustomer : ActivityAction.UnbanCustomer;
            var details = customer.IsBanned ? $"Banned customer: <a href='/customers/profile/{customer.Id}' class='customer-link'>{customer.Email}</a>" : $"Unbanned customer: <a href='/customers/profile/{customer.Id}' class='customer-link'>{customer.Email}</a>";
            await _logger.LogAsync(action, details, cId, cName, cRole, cFull);
            TempData["Success"] = $"Customer {(customer.IsBanned ? "banned" : "unbanned")} successfully.";
            return RedirectToAction(nameof(Index));
        }

        // POST: /customers/delete/{id}
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var customer = await _userManager.FindByIdAsync(id);
            if (customer == null) return NotFound();

            customer.RecordStatus = RecordStatus.Deleted;
            await _userManager.UpdateAsync(customer);

            var (cId, cName, cRole, cFull) = await CurrentEmployeeInfoAsync();
            await _logger.LogAsync(ActivityAction.DeleteCustomer, $"Soft-deleted customer <a href='/customers/profile/{customer.Id}' class='customer-link'>{customer.Email}</a>", cId, cName, cRole, cFull);
            
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { success = true });

            TempData["Success"] = "Customer account soft-deleted.";
            return RedirectToAction(nameof(Index));
        }

        // POST: /customers/restore/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(string id)
        {
            var customer = await _userManager.FindByIdAsync(id);
            if (customer == null) return NotFound();

            customer.RecordStatus = RecordStatus.Active;
            await _userManager.UpdateAsync(customer);

            var (cId, cName, cRole, cFull) = await CurrentEmployeeInfoAsync();
            await _logger.LogAsync(ActivityAction.UpdateTable, $"Restored customer <a href='/customers/profile/{customer.Id}' class='customer-link'>{customer.Email}</a>", cId, cName, cRole, cFull);

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { success = true });

            TempData["Success"] = "Customer account restored.";
            return RedirectToAction(nameof(Index));
        }
        // AJAX endpoint for UpdatableTable
        [HttpGet]
        public async Task<IActionResult> GetTableData(string? includeIds = null)
        {
            var data = await FetchCustomersTableData(includeIds);
            return Json(data);
        }

        private async Task<object> FetchCustomersTableData(string? includeIds = null)
        {
            var customers = await _userManager.GetUsersInRoleAsync("User");
            var sorted = customers.OrderByDescending(c => c.DateCreated).ToList();

            if (!string.IsNullOrEmpty(includeIds))
            {
                var extraIds = includeIds.Split(',').Select(s => s.Trim()).ToList();
                foreach (var id in extraIds)
                {
                    if (!sorted.Any(u => u.Id == id))
                    {
                        var extraUser = await _userManager.FindByIdAsync(id);
                        if (extraUser != null && await _userManager.IsInRoleAsync(extraUser, "User"))
                        {
                            sorted.Add(extraUser);
                        }
                    }
                }
            }

            var columns = new List<object>
            {
                new { id = "customer", updatable = false },
                new { id = "username", updatable = false },
                new { id = "email", updatable = false },
                new { id = "status", updatable = false },
                new { id = "joined", updatable = false },
                new { id = "actions", updatable = false }
            };

            var rows = sorted.ToDictionary(c => c.Id, c => (object)new
            {
                customer = string.IsNullOrWhiteSpace(c.FullName.Trim()) ? c.UserName : c.FullName,
                username = c.UserName,
                email = c.Email,
                status = c.IsBanned ? "Banned" : "Active",
                joined = c.DateCreated.ToString("MMM d, yyyy"),
                profilePicture = c.ProfilePicture,
                id = c.Id,
                isBanned = c.IsBanned,
                recordStatus = c.RecordStatus.ToString()
            });

            return new { columns, rows };
        }
    }
}
