using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MvcMusic.Data;
using MvcMusic.Models;

namespace MvcMusic.Controllers
{
    public class ProductsController : Controller
    {
        private readonly MvcMusicContext _context;

        public ProductsController(MvcMusicContext context)
        {
            _context = context;
        }

        // GET: /products (Admin/SuperAdmin dashboard - full CRUD list)
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> Index()
        {
            return View(await _context.Product.ToListAsync());
        }

        // GET: /products/user-index (User browse - read only)
        [AllowAnonymous]
        public async Task<IActionResult> UserIndex(string? category, string? search)
        {
            var products = _context.Product.AsQueryable();

            if (!string.IsNullOrEmpty(search))
                products = products.Where(p => p.Name.Contains(search) || p.Brand.Contains(search));

            if (!string.IsNullOrEmpty(category))
                products = products.Where(p => p.Category == category);

            ViewData["CurrentCategory"] = category;
            ViewData["CurrentSearch"] = search;
            ViewData["Categories"] = await _context.Product.Select(p => p.Category).Distinct().ToListAsync();

            return View(await products.ToListAsync());
        }

        // GET: /products/details/5 (Users, Admins, SuperAdmins)
        [AllowAnonymous]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var product = await _context.Product.FirstOrDefaultAsync(m => m.Id == id);
            if (product == null) return NotFound();

            return View(product);
        }

        // GET: /products/create
        [Authorize(Roles = "Admin,SuperAdmin")]
        public IActionResult Create()
        {
            return View();
        }

        // POST: /products/create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> Create([Bind("Id,Name,Category,Brand,Price,Stock,Description,Image,IsBanner,BannerDescription,Rating,SoldAmount")] Product product)
        {
            if (ModelState.IsValid)
            {
                _context.Add(product);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Product '{product.Name}' created successfully.";
                return RedirectToAction(nameof(Index));
            }
            return View(product);
        }

        // GET: /products/edit/5
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var product = await _context.Product.FindAsync(id);
            if (product == null) return NotFound();
            return View(product);
        }

        // POST: /products/edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Category,Brand,Price,Stock,Description,Image,IsBanner,BannerDescription,Rating,SoldAmount")] Product product)
        {
            if (id != product.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(product);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = $"Product '{product.Name}' updated successfully.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ProductExists(product.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(product);
        }

        // GET: /products/delete/5
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var product = await _context.Product.FirstOrDefaultAsync(m => m.Id == id);
            if (product == null) return NotFound();
            return View(product);
        }

        // POST: /products/delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var product = await _context.Product.FindAsync(id);
            if (product != null)
            {
                _context.Product.Remove(product);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Product deleted successfully.";
            }
            return RedirectToAction(nameof(Index));
        }

        private bool ProductExists(int id)
        {
            return _context.Product.Any(e => e.Id == id);
        }
    }
}
