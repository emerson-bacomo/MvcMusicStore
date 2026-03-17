using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MvcMusic.Data;
using MvcMusic.Models;
using MvcMusic.Utils;

namespace MvcMusic.Controllers
{
    [Authorize(Roles = "Admin,SuperAdmin")]
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
        public async Task<IActionResult> CustomerList()
        {
            var customers = await _userManager.GetUsersInRoleAsync("User");
            return View("CustomerList", customers.OrderByDescending(c => c.DateCreated).ToList());
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
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            ViewBag.Customer = customer;
            return View(orders);
        }

        // POST: /customers/toggle-ban/{id}
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
            var details = customer.IsBanned ? $"Banned customer: {customer.Email}" : $"Unbanned customer: {customer.Email}";
            await _logger.LogAsync(action, details, cId, cName, cRole, cFull);
            TempData["Success"] = $"Customer {(customer.IsBanned ? "banned" : "unbanned")} successfully.";
            return RedirectToAction(nameof(CustomerList));
        }
        // AJAX endpoint for UpdatableTable
        [HttpGet]
        public async Task<IActionResult> GetTableData()
        {
            var data = await FetchCustomersTableData();
            return Json(data);
        }

        private async Task<object> FetchCustomersTableData()
        {
            var customers = await _userManager.GetUsersInRoleAsync("User");
            var sorted = customers.OrderByDescending(c => c.DateCreated).ToList();

            var columns = new List<object>
            {
                new { id = "customer", label = "Customer", widthPercentage = "25%", sortable = true },
                new { id = "username", label = "Username", widthPercentage = "15%", sortable = true },
                new { id = "email", label = "Email", widthPercentage = "25%", sortable = true },
                new { id = "status", label = "Status", widthPercentage = "10%", sortable = true },
                new { id = "joined", label = "Joined", widthPercentage = "15%", sortable = true },
                new { id = "actions", label = "Actions", widthPercentage = "10%", sortable = false }
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
                isBanned = c.IsBanned
            });

            return new { columns, rows };
        }
    }
}
