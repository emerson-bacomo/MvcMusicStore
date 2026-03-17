using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MvcMusic.Models;

namespace MvcMusic.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly MvcMusic.Data.MvcMusicContext _context;

        public HomeController(ILogger<HomeController> logger, MvcMusic.Data.MvcMusicContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task<IActionResult> Index(string? category, string? search)
        {
            var products = _context.Product
                .Include(p => p.ProductImages)
                .Include(p => p.Category)
                .Include(p => p.Brand)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
                products = products.Where(p => p.Name.Contains(search) || (p.Brand != null && p.Brand.Name.Contains(search)));

            if (!string.IsNullOrEmpty(category))
                products = products.Where(p => p.Category != null && p.Category.Name == category);

            ViewData["CurrentCategory"] = category;
            ViewData["CurrentSearch"] = search;
            ViewData["Categories"] = await _context.Category.Select(c => c.Name).ToListAsync();

            var bannerProducts = await _context.Product
                .Include(p => p.ProductImages)
                .Include(p => p.Category)
                .Include(p => p.Brand)
                .Where(p => p.IsBanner)
                .ToListAsync();
            ViewData["BannerProducts"] = bannerProducts;

            return View(await products.ToListAsync());
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult About()
        {
            return View();
        }

        public IActionResult Terms()
        {
            return View();
        }

        public IActionResult Contact()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }


    }
}
