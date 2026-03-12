using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using MvcMusic.Models;

namespace MvcMusic.Controllers
{
    public class LandingController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public LandingController(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public IActionResult Index()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                if (User.IsInRole("Admin") || User.IsInRole("SuperAdmin") || User.IsInRole("Staff"))
                {
                    return RedirectToAction("Index", "AdminDashboard");
                }
                return RedirectToAction("Index", "Home");
            }
            return RedirectToAction("Hero");
        }

        public IActionResult Hero()
        {
            return View();
        }
    }
}
