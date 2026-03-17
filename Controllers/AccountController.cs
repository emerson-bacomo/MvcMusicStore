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

                    if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                        return Redirect(returnUrl);

                    return RedirectToRoleDashboard(roles);
                }
            }

            ModelState.AddModelError(string.Empty, "Invalid login attempt. Please check your credentials.");
            return View(model);
        }

        // GET: /account/register
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Register()
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToRoleDashboard();

            return View();
        }

        // POST: /account/register
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = new ApplicationUser
            {
                UserName = model.UserName,
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
                return RedirectToAction("Index", "Home");
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            return View(model);
        }

        // POST: /account/logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                var roles = await _userManager.GetRolesAsync(user);
                await _logger.LogAsync(ActivityAction.Logout, "User logged out", user.Id, user.UserName, roles.FirstOrDefault(), user.FullName);
            }

            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
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
                if (User.IsInRole("SuperAdmin") || User.IsInRole("Admin") || User.IsInRole("Staff"))
                    return RedirectToAction("Index", "AdminDashboard");
                return RedirectToAction("Index", "Home");
            }

            if (roles.Contains("SuperAdmin") || roles.Contains("Admin") || roles.Contains("Staff"))
                return RedirectToAction("Index", "AdminDashboard");
            return RedirectToAction("Index", "Home");
        }
    }
}
