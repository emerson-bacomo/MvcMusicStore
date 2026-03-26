using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MvcMusic.ViewModels;
using MvcMusic.Models;
using MvcMusic.Utils;

namespace MvcMusic.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IActivityLogService _logger;

        public AccountController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, IActivityLogService logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
        }

        // GET: /account/login
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToRoleDashboard();

            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        // POST: /account/login
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email) ?? await _userManager.FindByNameAsync(model.Email);
            
            if (user != null)
            {
                if (user.IsBanned)
                {
                    ModelState.AddModelError(string.Empty, "This account has been banned. Please contact an administrator.");
                    return View(model);
                }

                var result = await _signInManager.PasswordSignInAsync(
                    user.UserName!, model.Password, model.RememberMe, lockoutOnFailure: false);

                if (result.Succeeded)
                {
                    var roles = await _userManager.GetRolesAsync(user);
                    var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                    if (ip == "::1" || ip == "127.0.0.1") ip = "Localhost";
                    await _logger.LogAsync(ActivityAction.Login, $"User logged in from {ip}", user.Id, user.UserName, roles.FirstOrDefault(), user.FullName);

                    // Check if password change is required
                    if (user.RequiresPasswordChange)
                    {
                        return RedirectToAction(nameof(ChangePassword));
                    }

                    if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                        return Redirect(returnUrl);

                    return RedirectToRoleDashboard(roles);
                }
            }

            ModelState.AddModelError(string.Empty, "Invalid login attempt. Please check your credentials.");
            return View(model);
        }

        // GET: /account/change-password
        [HttpGet]
        public async Task<IActionResult> ChangePassword()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction(nameof(Login));
            return View();
        }

        // POST: /account/change-password
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(string newPassword)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction(nameof(Login));

            if (string.IsNullOrEmpty(newPassword) || newPassword.Length < 6)
            {
                ModelState.AddModelError(string.Empty, "Password must be at least 6 characters long.");
                return View();
            }

            // Remove existing password and set new one (bypass current password check to make it easier for temporary password transition)
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, newPassword);

            if (result.Succeeded)
            {
                user.RequiresPasswordChange = false;
                await _userManager.UpdateAsync(user);
                
                var roles = await _userManager.GetRolesAsync(user);
                await _logger.LogAsync(ActivityAction.UpdateTable, "User changed their temporary password.", user.Id, user.UserName, roles.FirstOrDefault(), user.FullName);
                
                TempData["Success"] = "Password changed successfully.";
                return RedirectToRoleDashboard(roles);
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            return View();
        }

        // GET: /account/register
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Register(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToRoleDashboard();

            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        // POST: /account/register
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (!ModelState.IsValid)
                return View(model);

            var baseUsername = $"{model.FirstName.Trim().ToLower().Replace(" ", "")}.{model.LastName.Trim().ToLower().Replace(" ", "")}";
            var uniqueUsername = baseUsername;
            int counter = 1;

            while (await _userManager.FindByNameAsync(uniqueUsername) != null)
            {
                uniqueUsername = $"{baseUsername}{counter}";
                counter++;
            }

            var user = new ApplicationUser
            {
                UserName = uniqueUsername,
                FirstName = model.FirstName.Trim(),
                LastName = model.LastName.Trim(),
                Email = model.Email,
                EmailConfirmed = true,
                DateCreated = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, "User");
                await _logger.LogAsync(ActivityAction.Register, "Registered a new account.", user.Id, user.UserName, "User", user.FullName);
                await _signInManager.SignInAsync(user, isPersistent: false);

                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    return Redirect(returnUrl);

                return RedirectToAction("Index", "Home");
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            return View(model);
        }

        // POST: /account/logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout(string? returnUrl = null)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                var roles = await _userManager.GetRolesAsync(user);
                await _logger.LogAsync(ActivityAction.Logout, "User logged out", user.Id, user.UserName, roles.FirstOrDefault(), user.FullName);
            }

            await _signInManager.SignOutAsync();

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction("Index", "Home");
        }

        // GET: /account/is-email-available
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> IsEmailAvailable(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return Json(true);
            
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null) return Json(true);
            
            // If user exists, check if it's the currently logged-in user (allow their own email)
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser != null && user.Id == currentUser.Id) return Json(true);
            
            return Json(false);
        }

        // GET: /account/access-denied
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        private IActionResult RedirectToRoleDashboard(IList<string>? roles = null)
        {
            if (roles == null)
            {
                if (User.IsInRole("SuperAdmin") || User.IsInRole("Admin") || 
                    User.IsInRole("StockStaff") || User.IsInRole("ProductStaff") || 
                    User.IsInRole("SalesStaff") || User.IsInRole("CustomerStaff"))
                {
                    return RedirectToAction("Index", "AdminDashboard");
                }
                return RedirectToAction("Index", "Home");
            }

            if (roles.Contains("SuperAdmin") || roles.Contains("Admin") || 
                roles.Any(r => r.EndsWith("Staff")))
            {
                return RedirectToAction("Index", "AdminDashboard");
            }
            return RedirectToAction("Index", "Home");
        }
    }
}
