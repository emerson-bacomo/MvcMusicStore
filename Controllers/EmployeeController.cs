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
    [Authorize(Roles = "SuperAdmin")]
    public class EmployeeController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly MvcMusicContext _context;
        private readonly IActivityLogService _logger;

        public EmployeeController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, MvcMusicContext context, IActivityLogService logger)
        {
            _userManager = userManager;
            _roleManager = roleManager;
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

        // GET: /employees
        // GET: /employees
        public async Task<IActionResult> EmployeeList()
        {
            var admins = await _userManager.GetUsersInRoleAsync("Admin");
            var staffs = await _userManager.GetUsersInRoleAsync("Staff");
            var superAdmins = await _userManager.GetUsersInRoleAsync("SuperAdmin");

            var allEmployees = admins.Select(u => (user: u, role: "Admin"))
                .Concat(staffs.Select(u => (user: u, role: "Staff")))
                .Concat(superAdmins.Select(u => (user: u, role: "SuperAdmin")))
                .OrderBy(e => e.role).ThenBy(e => e.user.UserName)
                .ToList();

            return View("EmployeeList", allEmployees);
        }

        // GET: /employees/create
        [HttpGet]
        public IActionResult Create() => View();

        // POST: /employees/create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(EmployeeCreateViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var existingEmail = await _userManager.FindByEmailAsync(model.Email);
            if (existingEmail != null)
            {
                ModelState.AddModelError(string.Empty, "An account with this email already exists.");
                return View(model);
            }

            var code = await GenerateEmployeeCodeAsync(model.Role);
            var user = new ApplicationUser
            {
                UserName = code,
                Email = model.Email,
                FirstName = model.FirstName,
                LastName = model.LastName,
                EmailConfirmed = true,
                DateCreated = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, model.Role);
                var (cId, cName, cRole) = await CurrentEmployeeInfoAsync();
                await _logger.LogAsync("Create Employee", $"Created {model.Role} account for {model.Email} ({code})", cId, cName, cRole);
                TempData["Success"] = $"Employee {code} ({model.FirstName} {model.LastName}) created successfully.";
                return RedirectToAction(nameof(EmployeeList));
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            return View(model);
        }

        // GET: /employees/edit/{id}
        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();
            var roles = await _userManager.GetRolesAsync(user);
            var vm = new EmployeeEditViewModel
            {
                Id = user.Id,
                FirstName = user.FirstName ?? "",
                LastName = user.LastName ?? "",
                Email = user.Email ?? "",
                Role = roles.FirstOrDefault() ?? "Staff",
                UserName = user.UserName
            };
            return View(vm);
        }

        // POST: /employees/edit/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EmployeeEditViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.FindByIdAsync(model.Id);
            if (user == null) return NotFound();

            user.FirstName = model.FirstName;
            user.LastName = model.LastName;
            user.Email = model.Email;

            await _userManager.UpdateAsync(user);

            // Update role
            var currentRoles = await _userManager.GetRolesAsync(user);
            var allowedRoles = new[] { "Admin", "Staff" };
            if (!currentRoles.Contains(model.Role) && allowedRoles.Contains(model.Role))
            {
                await _userManager.RemoveFromRolesAsync(user, currentRoles.Intersect(allowedRoles).ToArray());
                await _userManager.AddToRoleAsync(user, model.Role);
            }

            var (cId, cName, cRole) = await CurrentEmployeeInfoAsync();
            await _logger.LogAsync("Edit Employee", $"Edited employee {user.UserName} ({user.Email}), set role to {model.Role}", cId, cName, cRole);
            TempData["Success"] = "Employee updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        // POST: /employees/disable/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleDisable(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            user.IsDisabled = !user.IsDisabled;
            await _userManager.UpdateAsync(user);

            var (cId, cName, cRole) = await CurrentEmployeeInfoAsync();
            var action = user.IsDisabled ? "Disable Employee" : "Enable Employee";
            await _logger.LogAsync(action, $"{action}: {user.UserName} ({user.Email})", cId, cName, cRole);
            TempData["Success"] = $"Employee {(user.IsDisabled ? "disabled" : "enabled")} successfully.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /employees/reset-password/{id}
        [HttpGet]
        public async Task<IActionResult> ResetPassword(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();
            return View(new ResetPasswordViewModel { Id = id, UserName = user.UserName, NewPassword = "", ConfirmPassword = "" });
        }

        // POST: /employees/reset-password/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.FindByIdAsync(model.Id);
            if (user == null) return NotFound();

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);

            if (result.Succeeded)
            {
                var (cId, cName, cRole) = await CurrentEmployeeInfoAsync();
                await _logger.LogAsync("Reset Password", $"Password reset for {user.UserName} ({user.Email})", cId, cName, cRole);
                TempData["Success"] = $"Password for {user.UserName} reset successfully.";
                return RedirectToAction(nameof(EmployeeList));
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            return View(model);
        }

        // GET: /employees/delete/{id}
        [HttpGet]
        public async Task<IActionResult> Delete(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();
            var roles = await _userManager.GetRolesAsync(user);
            ViewBag.Role = roles.FirstOrDefault();
            return View(user);
        }

        // POST: /employees/delete/{id}
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var (cId, cName, cRole) = await CurrentEmployeeInfoAsync();
            await _logger.LogAsync("Delete Employee", $"Deleted employee {user.UserName} ({user.Email})", cId, cName, cRole);

            await _userManager.DeleteAsync(user);
            TempData["Success"] = "Employee account deleted.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /employees/activity-logs/{id?}
        [HttpGet]
        public async Task<IActionResult> ActivityLogs(string? id)
        {
            IQueryable<ActivityLog> query = _context.ActivityLog.OrderByDescending(l => l.Timestamp);

            ApplicationUser? employee = null;
            if (!string.IsNullOrEmpty(id))
            {
                employee = await _userManager.FindByIdAsync(id);
                if (employee != null)
                    query = query.Where(l => l.UserId == id);
            }

            ViewBag.Employee = employee;
            var logs = await query.Take(200).ToListAsync();
            return View(logs);
        }

        private async Task<string> GenerateEmployeeCodeAsync(string role)
        {
            var year = DateTime.UtcNow.ToString("yy");
            var prefix = role switch
            {
                "Admin" => "A",
                "Staff" => "S",
                "SuperAdmin" => "X",
                _ => "E"
            };

            var existing = await _context.Users
                .Where(u => u.UserName != null && u.UserName.StartsWith($"{year}-{prefix}"))
                .Select(u => u.UserName)
                .ToListAsync();

            int maxNum = existing
                .Select(c => int.TryParse(c!.Split('-').Last().TrimStart(prefix[0]), out var n) ? n : 0)
                .DefaultIfEmpty(0).Max();

            return $"{year}-{prefix}{(maxNum + 1):D4}";
        }
    }
}
