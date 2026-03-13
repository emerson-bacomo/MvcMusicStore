using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MvcMusic.Data;
using MvcMusic.Models;
using MvcMusic.Utils;

namespace MvcMusic.Controllers
{
    public class ProductsController : Controller
    {
        private readonly MvcMusicContext _context;
        private readonly IActivityLogService _logger;
        private readonly UserManager<ApplicationUser> _userManager;

        public ProductsController(MvcMusicContext context, IActivityLogService logger, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _logger = logger;
            _userManager = userManager;
        }

        private async Task<(string? id, string? name, string? role)> CurrentEmployeeInfoAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return (null, null, null);
            var roles = await _userManager.GetRolesAsync(user);
            return (user.Id, user.UserName, roles.FirstOrDefault());
        }

        // GET: /products/admin-products (Admin/SuperAdmin/Staff dashboard - specific access levels)
        [Authorize(Roles = "Admin,SuperAdmin,Staff")]
        public async Task<IActionResult> Index()
        {
            return View("Index", await _context.Product.Include(p => p.ProductImages).ToListAsync());
        }

        // GET: /products/details/5 (Users, Admins, SuperAdmins)
        [AllowAnonymous]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var product = await _context.Product.Include(p => p.ProductImages).FirstOrDefaultAsync(m => m.Id == id);
            if (product == null) return NotFound();

            product.ProductImages = product.ProductImages.OrderBy(p => p.SortOrder).ToList();

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
        public async Task<IActionResult> Create([Bind("Id,Name,Category,Brand,Price,Stock,Description,IsBanner,BannerDescription,Rating,SoldAmount")] Product product, List<IFormFile>? productImages, List<string>? imageUrls, string? primaryImage, string? imageOrder)
        {
            if (ModelState.IsValid)
            {
                if (productImages != null && productImages.Count > 0)
                {
                    foreach (var file in productImages)
                    {
                        var filePath = await SaveFile(file);
                        product.ProductImages.Add(new ProductImage { Url = filePath });
                    }
                }
                
                if (imageUrls != null && imageUrls.Count > 0)
                {
                    foreach (var url in imageUrls)
                    {
                        if (!string.IsNullOrWhiteSpace(url))
                            product.ProductImages.Add(new ProductImage { Url = url });
                    }
                }

                if (product.ProductImages.Any())
                {
                    var primary = product.ProductImages.FirstOrDefault(p => p.Url == primaryImage) ?? product.ProductImages.First();
                    primary.IsPrimary = true;
                }

                if (!string.IsNullOrEmpty(imageOrder))
                {
                    var orderArray = imageOrder.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i < orderArray.Length; i++)
                    {
                        var img = product.ProductImages.FirstOrDefault(p => p.Url == orderArray[i]);
                        if (img != null) img.SortOrder = i;
                    }
                }

                product.DateCreated = DateTime.UtcNow;
                _context.Add(product);
                await _context.SaveChangesAsync();
                var (cId, cName, cRole) = await CurrentEmployeeInfoAsync();
                await _logger.LogAsync("Create Product", $"Created product '{product.Name}' (ID: {product.Id})", cId, cName, cRole);
                TempData["Success"] = $"Product '{product.Name}' created successfully.";
                return RedirectToAction(nameof(Index));
            }
            return View(product);
        }

        // GET: /products/edit/5 (Admin, SuperAdmin, Staff)
        [Authorize(Roles = "Admin,SuperAdmin,Staff")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var product = await _context.Product.Include(p => p.ProductImages).FirstOrDefaultAsync(p => p.Id == id);
            if (product == null) return NotFound();
            product.ProductImages = product.ProductImages.OrderBy(p => p.SortOrder).ToList();
            return View(product);
        }

        // POST: /products/edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,SuperAdmin,Staff")]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Category,Brand,Price,Stock,Description,IsBanner,BannerDescription,Rating,SoldAmount")] Product product, List<IFormFile>? productImages, List<string>? existingImages, List<string>? imageUrls, string? deletedImages, string? primaryImage, string? imageOrder)
        {
            if (id != product.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    product.ProductImages.Clear();

                    if (existingImages != null && existingImages.Count > 0)
                    {
                        foreach (var url in existingImages)
                        {
                            if (!string.IsNullOrWhiteSpace(url))
                                product.ProductImages.Add(new ProductImage { Url = url });
                        }
                    }

                    if (productImages != null && productImages.Count > 0)
                    {
                        foreach (var file in productImages)
                        {
                            var filePath = await SaveFile(file);
                            product.ProductImages.Add(new ProductImage { Url = filePath });
                        }
                    }

                    if (imageUrls != null && imageUrls.Count > 0)
                    {
                        foreach (var url in imageUrls)
                        {
                            if (!string.IsNullOrWhiteSpace(url))
                                product.ProductImages.Add(new ProductImage { Url = url });
                        }
                    }

                    if (!string.IsNullOrEmpty(deletedImages))
                    {
                        try
                        {
                            var deletedFiles = System.Text.Json.JsonSerializer.Deserialize<List<string>>(deletedImages);
                            if (deletedFiles != null)
                            {
                                foreach (var fileUrl in deletedFiles)
                                {
                                    if (fileUrl.StartsWith("/uploads/"))
                                    {
                                        var physicalPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", fileUrl.TrimStart('/'));
                                        if (System.IO.File.Exists(physicalPath))
                                        {
                                            System.IO.File.Delete(physicalPath);
                                        }
                                    }
                                }
                            }
                        }
                        catch { /* Ignore deserialization errors */ }
                    }

                    var existingProduct = await _context.Product.Include(p => p.ProductImages).FirstOrDefaultAsync(p => p.Id == id);
                    if (existingProduct != null)
                    {
                        _context.Entry(existingProduct).CurrentValues.SetValues(product);
                        
                        // Sync navigation property
                        _context.ProductImage.RemoveRange(existingProduct.ProductImages);
                        foreach (var img in product.ProductImages)
                        {
                            existingProduct.ProductImages.Add(img);
                        }

                        if (existingProduct.ProductImages.Any())
                        {
                            var primary = existingProduct.ProductImages.FirstOrDefault(p => p.Url == primaryImage) ?? existingProduct.ProductImages.First();
                            foreach (var img in existingProduct.ProductImages) img.IsPrimary = false;
                            primary.IsPrimary = true;
                        }

                        if (!string.IsNullOrEmpty(imageOrder))
                        {
                            var orderArray = imageOrder.Split(',', StringSplitOptions.RemoveEmptyEntries);
                            for (int i = 0; i < orderArray.Length; i++)
                            {
                                var imgUrl = orderArray[i];
                                var img = existingProduct.ProductImages.FirstOrDefault(p => p.Url == imgUrl);
                                if (img != null) img.SortOrder = i;
                            }
                        }

                        existingProduct.DateModified = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                        var (cId2, cName2, cRole2) = await CurrentEmployeeInfoAsync();
                        await _logger.LogAsync("Edit Product", $"Edited product '{product.Name}' (ID: {id})", cId2, cName2, cRole2);
                        TempData["Success"] = $"Product '{product.Name}' updated successfully.";
                    }
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

        private async Task<string> SaveFile(IFormFile file)
        {
            var fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
            var uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "products");
            if (!Directory.Exists(uploadDir)) Directory.CreateDirectory(uploadDir);
            
            var filePath = Path.Combine(uploadDir, fileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }
            return "/uploads/products/" + fileName;
        }

        // GET: /products/delete/5
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var product = await _context.Product.Include(p => p.ProductImages).FirstOrDefaultAsync(m => m.Id == id);
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
                var (cId, cName, cRole) = await CurrentEmployeeInfoAsync();
                await _logger.LogAsync("Delete Product", $"Deleted product '{product.Name}' (ID: {id})", cId, cName, cRole);
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
