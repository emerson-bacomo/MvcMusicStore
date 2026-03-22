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
        public async Task<IActionResult> Index(string? category, string? brand, string? search)
        {
            var productsQuery = _context.Product
                .Include(p => p.ProductImages)
                .Include(p => p.Category)
                .Include(p => p.Brand)
                .Where(p => p.RecordStatus == RecordStatus.Active)
                .AsQueryable();

            if (!string.IsNullOrEmpty(category))
                productsQuery = productsQuery.Where(p => p.Category != null && p.Category.Name == category);

            if (!string.IsNullOrEmpty(brand))
                productsQuery = productsQuery.Where(p => p.Brand != null && p.Brand.Name == brand);

            var productsList = await productsQuery.ToListAsync();

            if (!string.IsNullOrEmpty(search))
            {
                var s = search.ToLower().Trim();
                productsList = productsList.Where(p => 
                    p.Name.ToLower().Contains(s) || 
                    (p.Brand != null && p.Brand.Name.ToLower().Contains(s)) ||
                    (p.Category != null && p.Category.Name.ToLower().Contains(s)) ||
                    p.DisplayPrice.ToLower().Contains(s) ||
                    p.DisplayStock.ToLower().Contains(s) ||
                    p.DisplaySold.ToLower().Contains(s) ||
                    p.DisplayRatingsCount.ToLower().Contains(s) ||
                    p.DisplayRating.ToLower().Contains(s)
                ).ToList();
            }

            ViewData["CurrentCategory"] = category;
            ViewData["CurrentBrand"] = brand;
            ViewData["CurrentSearch"] = search;
            ViewData["Categories"] = await _context.Category.Where(c => c.RecordStatus == RecordStatus.Active).Select(c => c.Name).ToListAsync();
            ViewData["Brands"] = await _context.Brand.Where(b => b.RecordStatus == RecordStatus.Active).Select(b => b.Name).ToListAsync();

            var bannerProducts = await _context.Product
                .Include(p => p.ProductImages)
                .Include(p => p.Category)
                .Include(p => p.Brand)
                .Where(p => p.IsBanner && p.RecordStatus == RecordStatus.Active)
                .ToListAsync();
            ViewData["BannerProducts"] = bannerProducts;

            return View(productsList);
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
