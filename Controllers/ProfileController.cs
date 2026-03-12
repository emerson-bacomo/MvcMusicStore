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

        // GET: /profile/edit
        [HttpGet]
        public async Task<IActionResult> Edit()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var vm = new ProfileEditViewModel
            {
                FirstName = user.FirstName ?? "",
                LastName = user.LastName ?? "",
                UserName = user.UserName,
                ProfilePicture = user.ProfilePicture
            };
            return View(vm);
        }

        // POST: /profile/edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ProfileEditViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            user.FirstName = model.FirstName;
            user.LastName = model.LastName;

            if (model.NewProfilePicture != null && model.NewProfilePicture.Length > 0)
            {
                var ext = Path.GetExtension(model.NewProfilePicture.FileName).ToLower();
                if (!new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" }.Contains(ext))
                {
                    ModelState.AddModelError(nameof(model.NewProfilePicture), "Unsupported file type.");
                    return View(model);
                }

                var uploadDir = Path.Combine(_env.WebRootPath, "uploads", "avatars");
                if (!Directory.Exists(uploadDir)) Directory.CreateDirectory(uploadDir);

                // Delete old picture
                if (!string.IsNullOrEmpty(user.ProfilePicture))
                {
                    var oldPath = Path.Combine(_env.WebRootPath, user.ProfilePicture.TrimStart('/'));
                    if (System.IO.File.Exists(oldPath)) System.IO.File.Delete(oldPath);
                }

                var fileName = user.Id + ext;
                var filePath = Path.Combine(uploadDir, fileName);
                using var stream = new FileStream(filePath, FileMode.Create);
                await model.NewProfilePicture.CopyToAsync(stream);
                user.ProfilePicture = "/uploads/avatars/" + fileName;
            }

            await _userManager.UpdateAsync(user);
            TempData["Success"] = "Profile updated successfully.";
            return RedirectToAction(nameof(Index));
        }
    }
}
