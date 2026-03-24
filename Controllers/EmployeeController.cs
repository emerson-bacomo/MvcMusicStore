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
                DateCreated = DateTime.UtcNow,
                RequiresPasswordChange = true
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, model.Role);
                var (cId, cName, cRole, cFull) = await CurrentEmployeeInfoAsync();
                await _logger.LogAsync(ActivityAction.CreateEmployee, $"Created {model.Role} account for <a href='/employees/edit/{user.Id}' class='employee-link'>{model.Email}</a> ({code})", cId, cName, cRole, cFull);
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
                Role = roles.FirstOrDefault() ?? "",
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
            var currentUser = await _userManager.GetUserAsync(User);
            var isCurrentSuperAdmin = await _userManager.IsInRoleAsync(currentUser!, "SuperAdmin");

            // Only SuperAdmin can change roles
            if (model.Role != currentRoles.FirstOrDefault() && isCurrentSuperAdmin)
            {
                // SuperAdmin cannot change their own role
                if (user.Id == currentUser!.Id)
                {
                    TempData["Error"] = "SuperAdmins cannot change their own role.";
                    return RedirectToAction(nameof(Edit), new { id = model.Id });
                }

                var allowedRoles = new[] { "Admin", "StockStaff", "ProductStaff", "SalesStaff", "CustomerStaff", "SuperAdmin" };
                if (allowedRoles.Contains(model.Role))
                {
                    await _userManager.RemoveFromRolesAsync(user, currentRoles);
                    await _userManager.AddToRoleAsync(user, model.Role);
                }
            }
            else if (model.Role != currentRoles.FirstOrDefault() && !isCurrentSuperAdmin)
            {
                TempData["Error"] = "Only SuperAdmins can change roles.";
            }

            var (cId, cName, cRole, cFull) = await CurrentEmployeeInfoAsync();
            await _logger.LogAsync(ActivityAction.EditEmployee, $"Edited employee <a href='/employees/edit/{user.Id}' class='employee-link'>{user.UserName} ({user.Email})</a>, set role to {model.Role}", cId, cName, cRole, cFull);
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
                ? $"<b>{actorDesc}</b> banned employee: <a href='/employees/edit/{user.Id}' class='employee-link'>{user.UserName} ({user.Email})</a>" 
                : $"<b>{actorDesc}</b> unbanned employee: <a href='/employees/edit/{user.Id}' class='employee-link'>{user.UserName} ({user.Email})</a>";
            await _logger.LogAsync(action, details, cId, cName, cRole, cFull);
            TempData["Success"] = $"Employee {(user.IsBanned ? "banned" : "unbanned")} successfully.";
            return RedirectToAction(nameof(Index));
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

            user.RecordStatus = RecordStatus.Deleted;
            await _userManager.UpdateAsync(user);

            var (cId, cName, cRole, cFull) = await CurrentEmployeeInfoAsync();
            await _logger.LogAsync(ActivityAction.DeleteEmployee, $"Soft-deleted employee <a href='/employees/edit/{user.Id}' class='employee-link'>{user.UserName} ({user.Email})</a>", cId, cName, cRole, cFull);
            
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { success = true });

            TempData["Success"] = "Employee account soft-deleted.";
            return RedirectToAction(nameof(Index));
        }

        // POST: /employees/restore/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            user.RecordStatus = RecordStatus.Active;
            await _userManager.UpdateAsync(user);

            var (cId, cName, cRole, cFull) = await CurrentEmployeeInfoAsync();
            await _logger.LogAsync(ActivityAction.UpdateTable, $"Restored employee <a href='/employees/edit/{user.Id}' class='employee-link'>{user.UserName} ({user.Email})</a>", cId, cName, cRole, cFull);

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { success = true });

        TempData["Success"] = "Employee account restored.";
            return RedirectToAction(nameof(Index));
        }

        // POST: /employees/reset-password/{id}
        [HttpPost]
        [Authorize(Roles = "SuperAdmin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return Json(new { success = false, message = "User not found." });

            // SuperAdmins cannot reset their own password through this endpoint (security best practice)
            // They should use the profile settings.
            var currentUser = await _userManager.GetUserAsync(User);
            if (user.Id == currentUser!.Id)
            {
                return Json(new { success = false, message = "You cannot reset your own password here. Use Profile Settings." });
            }

            // Generate a random password
            var charset = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()_+";
            var random = new Random();
            var newPassword = new string(Enumerable.Repeat(charset, 12).Select(s => s[random.Next(s.Length)]).ToArray());
            
            // Add at least one digit and one special if missing
            if (!newPassword.Any(char.IsDigit)) newPassword = newPassword.Substring(0, 11) + random.Next(10).ToString();
            if (!newPassword.Any(c => "!@#$%^&*()_+".Contains(c))) newPassword = newPassword.Substring(0, 10) + "!" + newPassword.Last();

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, newPassword);

            if (result.Succeeded)
            {
                user.RequiresPasswordChange = true;
                await _userManager.UpdateAsync(user);

                var (cId, cName, cRole, cFull) = await CurrentEmployeeInfoAsync();
                await _logger.LogAsync(ActivityAction.UpdateTable, $"Reset password for employee {user.UserName} ({user.Email})", cId, cName, cRole, cFull);

                return Json(new { success = true, newPassword = newPassword });
            }

            return Json(new { success = false, message = string.Join(", ", result.Errors.Select(e => e.Description)) });
        }

        // AJAX endpoint for UpdatableTable
        [HttpGet]
        public async Task<IActionResult> GetTableData(string? includeIds = null)
        {
            var admins = await _userManager.GetUsersInRoleAsync("Admin");
            var superAdmins = await _userManager.GetUsersInRoleAsync("SuperAdmin");
            var stockStaffs = await _userManager.GetUsersInRoleAsync("StockStaff");
            var productStaffs = await _userManager.GetUsersInRoleAsync("ProductStaff");
            var salesStaffs = await _userManager.GetUsersInRoleAsync("SalesStaff");
            var customerStaffs = await _userManager.GetUsersInRoleAsync("CustomerStaff");

            var allEmployeesRaw = admins.Concat(superAdmins)
                .Concat(stockStaffs).Concat(productStaffs).Concat(salesStaffs).Concat(customerStaffs)
                .DistinctBy(u => u.Id).ToList();

            if (!string.IsNullOrEmpty(includeIds))
            {
                var extraIds = includeIds.Split(',').Select(s => s.Trim()).ToList();
                foreach (var id in extraIds)
                {
                    if (!allEmployeesRaw.Any(u => u.Id == id))
                    {
                        var extraUser = await _userManager.FindByIdAsync(id);
                        if (extraUser != null)
                        {
                            // Verify they have an employee role
                            if (await _userManager.IsInRoleAsync(extraUser, "Admin") || 
                                await _userManager.IsInRoleAsync(extraUser, "SuperAdmin") ||
                                await _userManager.IsInRoleAsync(extraUser, "StockStaff") ||
                                await _userManager.IsInRoleAsync(extraUser, "ProductStaff") ||
                                await _userManager.IsInRoleAsync(extraUser, "SalesStaff") ||
                                await _userManager.IsInRoleAsync(extraUser, "CustomerStaff"))
                            {
                                allEmployeesRaw.Add(extraUser);
                            }
                        }
                    }
                }
            }

            var allEmployees = new List<(ApplicationUser user, string role)>();
            foreach(var u in allEmployeesRaw)
            {
                var role = admins.Any(a => a.Id == u.Id) ? "Admin" 
                         : superAdmins.Any(s => s.Id == u.Id) ? "SuperAdmin"
                         : stockStaffs.Any(s => s.Id == u.Id) ? "StockStaff"
                         : productStaffs.Any(s => s.Id == u.Id) ? "ProductStaff"
                         : salesStaffs.Any(s => s.Id == u.Id) ? "SalesStaff"
                         : customerStaffs.Any(s => s.Id == u.Id) ? "CustomerStaff"
                         : "";
                allEmployees.Add((u, role));
            }

            allEmployees = allEmployees
                .OrderBy(e => e.role).ThenBy(e => e.user.UserName)
                .ToList();

            var columns = new List<object>
            {
                new { id = "code", updatable = false },
                new { id = "name", updatable = false },
                new { id = "username", updatable = false },
                new { id = "email", updatable = false },
                new { id = "role", updatable = false },
                new { id = "status", updatable = false },
                new { id = "actions", updatable = false }
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
                firstName = string.IsNullOrEmpty(e.user.FirstName) ? "User" : e.user.FirstName,
                recordStatus = e.user.RecordStatus.ToString()
            });

            return Json(new { columns, rows });
        }

        private async Task<string> GenerateEmployeeCodeAsync(string role)
        {
            var year = DateTime.UtcNow.ToString("yy");
            var prefix = role switch
            {
                "Admin" => "A",
                "SuperAdmin" => "X",
                "StockStaff" => "T",
                "ProductStaff" => "P",
                "SalesStaff" => "L",
                "CustomerStaff" => "C",
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
