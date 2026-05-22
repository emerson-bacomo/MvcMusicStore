using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MvcMusic.Data;
using MvcMusic.Models;
using MvcMusic.ViewModels;
using Microsoft.EntityFrameworkCore;
using MvcMusic.Utils;

namespace MvcMusic.Controllers
{
    [Authorize(Roles = "SuperAdmin")]
    public class DemoController : Controller
    {
        private readonly MvcMusicContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IServiceProvider _serviceProvider;
        private readonly IDemoLockService _lockService;

        public DemoController(MvcMusicContext context, UserManager<ApplicationUser> userManager, IServiceProvider serviceProvider, IDemoLockService lockService)
        {
            _context = context;
            _userManager = userManager;
            _serviceProvider = serviceProvider;
            _lockService = lockService;
        }

        public IActionResult Index()
        {

            var sessionId = GetSessionId();
            _lockService.TryClaim(sessionId);
            
            var products = DataSeeder.GetBaseProducts();
            var viewModel = new DemoViewModel
            {
                SuperAdminPreview = new ApplicationUser
                {
                    UserName = $"{DateTime.UtcNow:yy}-X0001",
                    Email = "superadmin@nightcord.com",
                    FirstName = "Super",
                    LastName = "Admin"
                },
                EmployeesPreview = GetEmployeesPreview(),
                CustomersPreview = GetCustomersPreview(),
                ProductsPreview = products.Take(5).ToList(),
                CategoriesPreview = products.Select(p => p.Category?.Name).OfType<string>().Distinct().ToList(),
                BrandsPreview = products.Select(p => p.Brand?.Name).OfType<string>().Distinct().ToList(),
                OrdersPreview = GetOrdersPreview(),
                LockInfo = _lockService.GetLockInfo()
            };

            ViewBag.UserIdentity = sessionId;
            return View(viewModel);
        }

        [HttpPost]
        public IActionResult Heartbeat()
        {
            var identity = GetSessionId();
            _lockService.TryClaim(identity); // Automatically claim if free, or refresh if owner
            _lockService.RefreshHeartbeat(identity);
            return Ok();
        }

        [HttpGet]
        public IActionResult Status()
        {
            var info = _lockService.GetLockInfo();
            var identity = GetSessionId();
            return Json(new { 
                isLocked = info.IsLocked, 
                isOwner = info.OwnerSessionId == identity,
                remainingSeconds = info.RemainingSeconds
            });
        }

        private string GetSessionId()
        {
            const string CookieName = "nc_demo_identity";
            var identity = Request.Cookies[CookieName];

            if (string.IsNullOrEmpty(identity))
            {
                identity = Guid.NewGuid().ToString();
                var options = new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddHours(2),
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Strict
                };
                Response.Cookies.Append(CookieName, identity, options);
            }

            return identity;
        }

        private List<OrderPreview> GetOrdersPreview()
        {
            return new List<OrderPreview>
            {
                new OrderPreview { Id = 101, CustomerName = "Maria Santos", Date = DateTime.UtcNow.AddDays(-2).ToString("yyyy-MM-dd"), Total = 12500.00m, Status = "Delivered" },
                new OrderPreview { Id = 102, CustomerName = "John Cruz", Date = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd"), Total = 4500.50m, Status = "OnDelivery" },
                new OrderPreview { Id = 103, CustomerName = "Ana Reyes", Date = DateTime.UtcNow.ToString("yyyy-MM-dd"), Total = 899.00m, Status = "Pending" }
            };
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reset(DataSeeder.SeedOptions options)
        {
            var sessionId = GetSessionId();
            if (!_lockService.IsLockOwner(sessionId))
            {
                TempData["Error"] = "Access Denied: You do not own the current testing lock.";
                return RedirectToAction("Index");
            }

            try
            {
                // Wipe database
                await _context.Database.EnsureDeletedAsync();
                await _context.Database.MigrateAsync();

                // Seed with options
                await DataSeeder.SeedAllAsync(_serviceProvider, options);

                TempData["Success"] = "Database successfully reset and re-seeded!";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"An error occurred during reset: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        private List<ApplicationUser> GetEmployeesPreview()
        {
            var year = DateTime.UtcNow.ToString("yy");
            return new List<ApplicationUser>
            {
                new ApplicationUser { UserName = $"{year}-A0001", Email = "admin@nightcord.com", FirstName = "Alice", LastName = "Rivera" },
                new ApplicationUser { UserName = $"{year}-T0001", Email = "stockstaff@nightcord.com", FirstName = "Tina", LastName = "Stone" },
                new ApplicationUser { UserName = $"{year}-P0001", Email = "productstaff@nightcord.com", FirstName = "Paula", LastName = "Cruz" },
                new ApplicationUser { UserName = $"{year}-L0001", Email = "salesstaff@nightcord.com", FirstName = "Leon", LastName = "Drake" },
                new ApplicationUser { UserName = $"{year}-C0001", Email = "customerstaff@nightcord.com", FirstName = "Clara", LastName = "Vance" }
            };
        }

        private List<ApplicationUser> GetCustomersPreview()
        {
            var customerNames = new[] { "Maria Santos", "John Cruz", "Ana Reyes", "Miguel Torres", "Sofia Lim", "James Park", "Elena Gomez", "David Kim" };
            var list = new List<ApplicationUser>();
            for (int i = 0; i < customerNames.Length; i++)
            {
                var nameParts = customerNames[i].Split(' ');
                list.Add(new ApplicationUser
                {
                    UserName = $"customer{i + 1}",
                    Email = $"customer{i + 1}@gmail.com",
                    FirstName = nameParts[0],
                    LastName = nameParts[1]
                });
            }
            return list.Take(5).ToList();
        }
    }
}
