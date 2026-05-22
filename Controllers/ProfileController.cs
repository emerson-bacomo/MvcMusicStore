using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MvcMusic.Models;
using MvcMusic.ViewModels;

namespace MvcMusic.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _env;

        public ProfileController(UserManager<ApplicationUser> userManager, IWebHostEnvironment env)
        {
            _userManager = userManager;
            _env = env;
        }

        // GET: /profile
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var roles = await _userManager.GetRolesAsync(user);
            ViewBag.Roles = roles;
            return View(user);
        }

        // GET: /profile/settings
        [HttpGet]
        public async Task<IActionResult> Settings()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var vm = new SettingsViewModel
            {
                Email = user.Email ?? "",
                Username = user.UserName
            };
            return View(vm);
        }

        // POST: /profile/settings
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Settings(SettingsViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            if (!ModelState.IsValid) return View(model);

            // 1. Update Email
            if (user.Email != model.Email)
            {
                var existing = await _userManager.FindByEmailAsync(model.Email);
                if (existing != null && existing.Id != user.Id)
                {
                    ModelState.AddModelError("Email", "Email is already taken.");
                    return View(model);
                }
                user.Email = model.Email;
            }

            // 2. Update Username (Customer only)
            if (User.IsInRole("Customer") && user.UserName != model.Username && !string.IsNullOrEmpty(model.Username))
            {
                var existing = await _userManager.FindByNameAsync(model.Username);
                if (existing != null && existing.Id != user.Id)
                {
                    ModelState.AddModelError("Username", "Username is already taken.");
                    return View(model);
                }
                user.UserName = model.Username;
            }

            // 3. Update Password
            if (!string.IsNullOrEmpty(model.NewPassword))
            {
                if (User.IsInRole("SuperAdmin"))
                {
                    ModelState.AddModelError("CurrentPassword", "SuperAdmin password cannot be changed.");
                    return View(model);
                }

                if (string.IsNullOrEmpty(model.CurrentPassword))
                {
                    ModelState.AddModelError("CurrentPassword", "Current password is required to change password.");
                    return View(model);
                }

                var changeResult = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
                if (!changeResult.Succeeded)
                {
                    foreach (var error in changeResult.Errors)
                        ModelState.AddModelError("CurrentPassword", error.Description);
                    return View(model);
                }
            }

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                TempData["Success"] = "Settings updated successfully.";
                return RedirectToAction(nameof(Index));
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);

            return View(model);
        }

        // POST: /profile/update-avatar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateAvatar(IFormFile avatar)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Json(new { success = false, message = "Not authorized" });

            if (avatar == null || avatar.Length == 0) return Json(new { success = false, message = "No file uploaded" });

            var ext = Path.GetExtension(avatar.FileName).ToLower();
            if (!new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" }.Contains(ext))
                return Json(new { success = false, message = "Unsupported file type" });

            var uploadDir = Path.Combine(_env.WebRootPath, "uploads", "avatars");
            if (!Directory.Exists(uploadDir)) Directory.CreateDirectory(uploadDir);

            // Delete old
            if (!string.IsNullOrEmpty(user.ProfilePicture))
            {
                var oldPath = Path.Combine(_env.WebRootPath, user.ProfilePicture.TrimStart('/'));
                if (System.IO.File.Exists(oldPath)) System.IO.File.Delete(oldPath);
            }

            var fileName = user.Id + "_" + DateTime.UtcNow.Ticks + ext;
            var filePath = Path.Combine(uploadDir, fileName);
            using var stream = new FileStream(filePath, FileMode.Create);
            await avatar.CopyToAsync(stream);
            user.ProfilePicture = "/uploads/avatars/" + fileName;

            await _userManager.UpdateAsync(user);
            return Json(new { success = true, url = user.ProfilePicture });
        }

        // POST: /profile/update-name
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateName(string firstName, string lastName)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Json(new { success = false, message = "Not authorized" });

            if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
                return Json(new { success = false, message = "Name fields cannot be empty" });

            user.FirstName = firstName;
            user.LastName = lastName;

            await _userManager.UpdateAsync(user);
            return Json(new { success = true, fullName = user.FullName });
        }
    }
}
