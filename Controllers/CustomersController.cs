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

        private async Task<(string? id, string? name, string? role)> CurrentEmployeeInfoAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return (null, null, null);
            var roles = await _userManager.GetRolesAsync(user);
            return (user.Id, user.UserName, roles.FirstOrDefault());
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

            customer.IsDisabled = !customer.IsDisabled;
            await _userManager.UpdateAsync(customer);

            var (cId, cName, cRole) = await CurrentEmployeeInfoAsync();
            var action = customer.IsDisabled ? "Ban Customer" : "Unban Customer";
            await _logger.LogAsync(action, $"{action}: {customer.Email}", cId, cName, cRole);
            TempData["Success"] = $"Customer {(customer.IsDisabled ? "banned" : "unbanned")} successfully.";
            return RedirectToAction(nameof(CustomerList));
        }
    }
}
