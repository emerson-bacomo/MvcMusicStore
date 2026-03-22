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

using System.Text.Json;

namespace MvcMusic.Controllers
{
    public class UpdateTableRequest
    {
        public Dictionary<string, Dictionary<string, string>>? Changes { get; set; }
    }

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

        private async Task<(string? id, string? name, string? role, string? fullName)> CurrentEmployeeInfoAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return (null, null, null, null);
            var roles = await _userManager.GetRolesAsync(user);
            return (user.Id, user.UserName, roles.FirstOrDefault(), user.FullName);
        }

        // GET: /products/admin-products
        [Authorize(Roles = "Admin,SuperAdmin,Staff")]
        public IActionResult Index()
        {
            return View("Index");
        }

        [HttpGet]
        [Authorize(Roles = "Admin,SuperAdmin,Staff")]
        public async Task<IActionResult> GetTableData(int? categoryId = null, int? brandId = null, string? includeIds = null)
        {
            var query = _context.Product
                .Include(p => p.ProductImages)
                .Include(p => p.Category)
                .Include(p => p.Brand)
                .AsQueryable();

            if (categoryId.HasValue) query = query.Where(p => p.CategoryId == categoryId.Value);
            if (brandId.HasValue) query = query.Where(p => p.BrandId == brandId.Value);

            if (!string.IsNullOrEmpty(includeIds))
            {
                var extraIds = includeIds.Split(',').Select(s => int.TryParse(s, out int id) ? id : 0).Where(id => id > 0).ToList();
                if (extraIds.Any())
                {
                    query = _context.Product
                        .Include(p => p.ProductImages).Include(p => p.Category).Include(p => p.Brand)
                        .Where(p => extraIds.Contains(p.Id) || (
                            (!categoryId.HasValue || p.CategoryId == categoryId.Value) && 
                            (!brandId.HasValue || p.BrandId == brandId.Value)
                        ));
                }
            }

            var validationRules = ValidationHelper.GetValidationRules(typeof(Product));
            var products = await query.ToListAsync();
            var categories = await _context.Category.Where(c => c.RecordStatus == RecordStatus.Active).ToListAsync();
            var brands = await _context.Brand.Where(b => b.RecordStatus == RecordStatus.Active).ToListAsync();

            var columns = new List<object>
            {
                new { id = "image", updatable = false },
                new { 
                    id = "name", 
                    updatable = false, 
                    validation = validationRules.GetValueOrDefault("name")
                },
                new { 
                    id = "category", 
                    updatable = true, 
                    type = "select", 
                    options = categories.Select(c => new { value = c.Id, label = c.Name }),
                    validation = validationRules.GetValueOrDefault("categoryid")
                },
                new { 
                    id = "brand", 
                    updatable = true, 
                    type = "select", 
                    options = brands.Select(b => new { value = b.Id, label = b.Name }),
                    validation = validationRules.GetValueOrDefault("brandid")
                },
                new { 
                    id = "price", 
                    updatable = true,
                    isNumeric = true,
                    validation = validationRules.GetValueOrDefault("price")
                },
                new { 
                    id = "stock", 
                    updatable = true,
                    isNumeric = true,
                    validation = validationRules.GetValueOrDefault("stock")
                },
                new { id = "status", updatable = false },
                new { id = "actions", updatable = false }
            };

            var rows = new Dictionary<string, object>();
            foreach (var p in products)
            {
                var primaryImage = p.ProductImages.FirstOrDefault(img => img.IsPrimary)?.Url ?? p.ProductImages.FirstOrDefault()?.Url;
                rows[p.Id.ToString()] = new {
                    id = p.Id,
                    image = new { image = primaryImage, isBanner = p.IsBanner },
                    name = p.Name,
                    category = p.CategoryId,
                    categoryLabel = p.Category?.Name ?? "Uncategorized",
                    brand = p.BrandId,
                    brandLabel = p.Brand?.Name ?? "No Brand",
                    price = p.Price,
                    stock = p.Stock,
                    status = p.Stock > 0 ? "Available" : "Sold Out",
                    recordStatus = p.RecordStatus.ToString(),
                    _isExtra = !string.IsNullOrEmpty(includeIds) && (
                        (categoryId.HasValue && p.CategoryId != categoryId.Value) || 
                        (brandId.HasValue && p.BrandId != brandId.Value)
                    ) && includeIds.Split(',').Contains(p.Id.ToString())
                };
            }

            return Json(new {
                columns = columns,
                rows = rows,
                updateRequest = Url.Action("UpdateTableData")
            });
        }

        [HttpPost]
        [Authorize(Roles = "Admin,SuperAdmin,Staff")]
        public async Task<IActionResult> UpdateTableData([FromBody] UpdateTableRequest request)
        {
            if (request == null || request.Changes == null) return BadRequest();

            foreach(var rowChange in request.Changes)
            {
                if (int.TryParse(rowChange.Key, out int id))
                {
                    var product = await _context.Product.FindAsync(id);
                    if (product != null)
                    {
                        foreach(var colChange in rowChange.Value)
                        {
                            var colName = colChange.Key.ToLower();
                            var valueStr = colChange.Value?.ToString();

                            if (colName == "category" && int.TryParse(valueStr, out int catId)) product.CategoryId = catId;
                            else if (colName == "brand" && int.TryParse(valueStr, out int brId)) product.BrandId = brId;
                            else if (colName == "name") product.Name = valueStr ?? product.Name;
                            else if (colName == "price" && double.TryParse(valueStr, out double price)) product.Price = price;
                            else if (colName == "stock" && int.TryParse(valueStr, out int stock)) product.Stock = stock;
                        }
                        
                        if (!TryValidateModel(product))
                        {
                            return BadRequest(ModelState);
                        }
                    }
                }
            }
            await _context.SaveChangesAsync();
            
            var (cId, cName, cRole, cFullName) = await CurrentEmployeeInfoAsync();
            await _logger.LogAsync(ActivityAction.UpdateTable, $"Performed mass update on {request.Changes.Count} products.", cId, cName, cRole, cFullName);

            return Json(new { success = true });
        }

        // GET: /products/details/5
        [AllowAnonymous]
        public async Task<IActionResult> Details(int? id)
        {
            var product = await _context.Product
                .Include(p => p.ProductImages)
                .Include(p => p.Category)
                .Include(p => p.Brand)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (product == null) return NotFound();

            ViewBag.Categories = await _context.Category.Where(c => c.RecordStatus == RecordStatus.Active).ToListAsync();
            ViewBag.Brands = await _context.Brand.Where(b => b.RecordStatus == RecordStatus.Active).ToListAsync();

            product.ProductImages = product.ProductImages.OrderBy(p => p.SortOrder).ToList();

            return View(product);
        }

        [HttpPost]
        [Authorize(Roles = "Admin,SuperAdmin,Staff")]
        public async Task<IActionResult> UpdateDetails(int id, [Bind("Id,Name,CategoryId,BrandId,Price,Stock,Description,IsBanner,BannerDescription,Rating,SoldAmount")] Product product, List<IFormFile>? productImages, List<string>? existingImages, List<string>? imageUrls, string? deletedImages, string? primaryImage, string? imageOrder)
        {
            if (id != product.Id) return Json(new { success = false, message = "Id mismatch" });

            if (ModelState.IsValid)
            {
                try
                {
                    product.ProductImages.Clear();
                    if (existingImages != null) foreach (var url in existingImages) if (!string.IsNullOrWhiteSpace(url)) product.ProductImages.Add(new ProductImage { Url = url });
                    if (productImages != null) foreach (var file in productImages) { var filePath = await SaveFile(file); product.ProductImages.Add(new ProductImage { Url = filePath }); }
                    if (imageUrls != null) foreach (var url in imageUrls) if (!string.IsNullOrWhiteSpace(url)) product.ProductImages.Add(new ProductImage { Url = url });

                    if (!string.IsNullOrEmpty(deletedImages))
                    {
                        var deletedFiles = System.Text.Json.JsonSerializer.Deserialize<List<string>>(deletedImages);
                        if (deletedFiles != null)
                        {
                            foreach (var fileUrl in deletedFiles)
                            {
                                if (fileUrl.StartsWith("/uploads/"))
                                {
                                    var physicalPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", fileUrl.TrimStart('/'));
                                    if (System.IO.File.Exists(physicalPath)) System.IO.File.Delete(physicalPath);
                                }
                            }
                        }
                    }

                    var existingProduct = await _context.Product.Include(p => p.ProductImages).FirstOrDefaultAsync(p => p.Id == id);
                    if (existingProduct != null)
                    {
                        _context.Entry(existingProduct).CurrentValues.SetValues(product);
                        _context.ProductImage.RemoveRange(existingProduct.ProductImages);
                        foreach (var img in product.ProductImages) existingProduct.ProductImages.Add(img);

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
                                var img = existingProduct.ProductImages.FirstOrDefault(p => p.Url == orderArray[i]);
                                if (img != null) img.SortOrder = i;
                            }
                        }

                        existingProduct.DateModified = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                        var (cId, cName, cRole, cFull) = await CurrentEmployeeInfoAsync();
                        await _logger.LogAsync(ActivityAction.EditProduct, $"Edited product <a href='/products/details/{id}' class='product-link'>{product.Name}</a> in-place.", cId, cName, cRole, cFull);
                        return Json(new { success = true, message = "Product updated successfully." });
                    }
                    return Json(new { success = false, message = "Product not found." });
                }
                catch (Exception ex)
                {
                    return Json(new { success = false, message = ex.Message });
                }
            }
            return Json(new { success = false, message = "Invalid model state.", errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });
        }

        // GET: /products/create
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> Create()
        {
            ViewBag.Categories = await _context.Category.Where(c => c.RecordStatus == RecordStatus.Active).ToListAsync();
            ViewBag.Brands = await _context.Brand.Where(b => b.RecordStatus == RecordStatus.Active).ToListAsync();
            return View();
        }

        // POST: /products/create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> Create([Bind("Id,Name,CategoryId,BrandId,Price,Stock,Description,IsBanner,BannerDescription,Rating,SoldAmount")] Product product, List<IFormFile>? productImages, List<string>? imageUrls, string? primaryImage, string? imageOrder)
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
                var (cId, cName, cRole, cFull) = await CurrentEmployeeInfoAsync();
                await _logger.LogAsync(ActivityAction.CreateProduct, $"Created product <a href='/products/details/{product.Id}' class='product-link'>{product.Name}</a>.", cId, cName, cRole, cFull);
                TempData["Success"] = $"Product '{product.Name}' created successfully.";
                return RedirectToAction(nameof(Index));
            }
            return View(product);
        }

        // GET: /products/edit/5
        [Authorize(Roles = "Admin,SuperAdmin,Staff")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var product = await _context.Product.Include(p => p.ProductImages).FirstOrDefaultAsync(p => p.Id == id);
            if (product == null) return NotFound();
            
            ViewBag.Categories = await _context.Category.Where(c => c.RecordStatus == RecordStatus.Active).ToListAsync();
            ViewBag.Brands = await _context.Brand.Where(b => b.RecordStatus == RecordStatus.Active).ToListAsync();
            
            product.ProductImages = product.ProductImages.OrderBy(p => p.SortOrder).ToList();
            return View(product);
        }

        // POST: /products/edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,SuperAdmin,Staff")]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,CategoryId,BrandId,Price,Stock,Description,IsBanner,BannerDescription,Rating,SoldAmount")] Product product, List<IFormFile>? productImages, List<string>? existingImages, List<string>? imageUrls, string? deletedImages, string? primaryImage, string? imageOrder)
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
                        var (cId2, cName2, cRole2, cFull2) = await CurrentEmployeeInfoAsync();
                        await _logger.LogAsync(ActivityAction.EditProduct, $"Edited product <a href='/products/details/{id}' class='product-link'>{product.Name}</a>.", cId2, cName2, cRole2, cFull2);
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



        // POST: /products/restore/5
        [HttpPost]
        [Authorize(Roles = "Admin,SuperAdmin,Staff")]
        public async Task<IActionResult> Restore(int id)
        {
            var product = await _context.Product.FindAsync(id);
            if (product != null)
            {
                var (cId, cName, cRole, cFull) = await CurrentEmployeeInfoAsync();
                await _logger.LogAsync(ActivityAction.UpdateTable, $"Restored product <a href='/products/details/{id}' class='product-link'>{product.Name}</a>", cId, cName, cRole, cFull);
                product.RecordStatus = RecordStatus.Active;
                await _context.SaveChangesAsync();
                
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return Json(new { success = true });

                TempData["Success"] = "Product restored successfully.";
            }
            return RedirectToAction(nameof(Index));
        }

        // POST: /products/delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,SuperAdmin,Staff")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var product = await _context.Product.FindAsync(id);
            if (product != null)
            {
                var (cId, cName, cRole, cFull) = await CurrentEmployeeInfoAsync();
                await _logger.LogAsync(ActivityAction.DeleteProduct, $"Deleted product <a href='/products/details/{id}' class='product-link'>{product.Name}</a>", cId, cName, cRole, cFull);
                product.RecordStatus = RecordStatus.Deleted;
                await _context.SaveChangesAsync();
                
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return Json(new { success = true });

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
