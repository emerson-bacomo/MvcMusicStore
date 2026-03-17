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

        private async Task<(string? id, string? name, string? role, string? fullName)> CurrentEmployeeInfoAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return (null, null, null, null);
            var roles = await _userManager.GetRolesAsync(user);
            return (user.Id, user.UserName, roles.FirstOrDefault(), user.FullName);
        }

        // GET: /employees
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            ViewBag.CurrentUserId = user?.Id;
            return View();
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
                var (cId, cName, cRole, cFull) = await CurrentEmployeeInfoAsync();
                await _logger.LogAsync(ActivityAction.CreateEmployee, $"Created {model.Role} account for {model.Email} ({code})", cId, cName, cRole, cFull);
                TempData["Success"] = $"Employee {code} ({model.FirstName} {model.LastName}) created successfully.";
                return RedirectToAction(nameof(Index));
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

            var (cId, cName, cRole, cFull) = await CurrentEmployeeInfoAsync();
            await _logger.LogAsync(ActivityAction.EditEmployee, $"Edited employee {user.UserName} ({user.Email}), set role to {model.Role}", cId, cName, cRole, cFull);
            TempData["Success"] = "Employee updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        // POST: /employees/ban/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleBan(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            user.IsBanned = !user.IsBanned;
            await _userManager.UpdateAsync(user);

            var (cId, cName, cRole, cFull) = await CurrentEmployeeInfoAsync();
            var actorDesc = !string.IsNullOrEmpty(cFull) ? $"{cFull} ({cName})" : (cName ?? "SuperAdmin");
            var action = user.IsBanned ? ActivityAction.BanEmployee : ActivityAction.UnbanEmployee;
            var details = user.IsBanned 
                ? $"<b>{actorDesc}</b> banned employee: {user.UserName} ({user.Email})" 
                : $"<b>{actorDesc}</b> unbanned employee: {user.UserName} ({user.Email})";
            await _logger.LogAsync(action, details, cId, cName, cRole, cFull);
            TempData["Success"] = $"Employee {(user.IsBanned ? "banned" : "unbanned")} successfully.";
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
                var (cId, cName, cRole, cFull) = await CurrentEmployeeInfoAsync();
                await _logger.LogAsync(ActivityAction.ResetPassword, $"Password reset for {user.UserName} ({user.Email})", cId, cName, cRole, cFull);
                TempData["Success"] = $"Password for {user.UserName} reset successfully.";
                return RedirectToAction(nameof(Index));
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

            var (cId, cName, cRole, cFull) = await CurrentEmployeeInfoAsync();
            await _logger.LogAsync(ActivityAction.DeleteEmployee, $"Deleted employee {user.UserName} ({user.Email})", cId, cName, cRole, cFull);

            await _userManager.DeleteAsync(user);
            
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { success = true });

            TempData["Success"] = "Employee account deleted.";
            return RedirectToAction(nameof(Index));
        }

        // AJAX endpoint for UpdatableTable
        [HttpGet]
        public async Task<IActionResult> GetTableData()
        {
            var admins = await _userManager.GetUsersInRoleAsync("Admin");
            var staffs = await _userManager.GetUsersInRoleAsync("Staff");
            var superAdmins = await _userManager.GetUsersInRoleAsync("SuperAdmin");

            var allEmployees = admins.Select(u => (user: u, role: "Admin"))
                .Concat(staffs.Select(u => (user: u, role: "Staff")))
                .Concat(superAdmins.Select(u => (user: u, role: "SuperAdmin")))
                .OrderBy(e => e.role).ThenBy(e => e.user.UserName)
                .ToList();

            var columns = new List<object>
            {
                new { id = "code", label = "Code", widthPercentage = "10%", sortable = true },
                new { id = "name", label = "Name", widthPercentage = "20%", sortable = true },
                new { id = "username", label = "Username", widthPercentage = "15%", sortable = true },
                new { id = "email", label = "Email", widthPercentage = "20%", sortable = true },
                new { id = "role", label = "Role", widthPercentage = "10%", sortable = true },
                new { id = "status", label = "Status", widthPercentage = "10%", sortable = true },
                new { id = "actions", label = "Actions", widthPercentage = "15%", sortable = false }
            };

            var rows = allEmployees.ToDictionary(e => e.user.Id, e => (object)new
            {
                code = e.user.UserName,
                name = $"{e.user.FirstName} {e.user.LastName}",
                username = e.user.UserName,
                email = e.user.Email,
                role = e.role,
                status = e.user.IsBanned ? "Banned" : "Active",
                id = e.user.Id,
                isBanned = e.user.IsBanned,
                profilePicture = e.user.ProfilePicture,
                firstName = string.IsNullOrEmpty(e.user.FirstName) ? "User" : e.user.FirstName
            });

            return Json(new { columns, rows });
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
