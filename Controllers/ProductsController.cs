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
        public Dictionary<string, Dictionary<string, object>>? Changes { get; set; }
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
        [Authorize(Roles = "Admin,SuperAdmin,StockStaff,ProductStaff,SalesStaff")]
        public async Task<IActionResult> Index(int? categoryId = null, int? brandId = null)
        {
            var user = await _userManager.GetUserAsync(User);
            var roles = await _userManager.GetRolesAsync(user!);
            ViewBag.UserRole = roles.FirstOrDefault() ?? "";

            if (categoryId.HasValue) ViewBag.Category = await _context.Category.FindAsync(categoryId.Value);
            if (brandId.HasValue) ViewBag.Brand = await _context.Brand.FindAsync(brandId.Value);

            ViewBag.AllCategories = await _context.Category.Where(c => c.RecordStatus == RecordStatus.Active).OrderByDescending(c => c.Id).Select(c => new { c.Id, c.Name }).ToListAsync();
            ViewBag.AllBrands = await _context.Brand.Where(b => b.RecordStatus == RecordStatus.Active).OrderByDescending(b => b.Id).Select(b => new { b.Id, b.Name }).ToListAsync();

            return View("Index");
        }

        [HttpGet]
        [Authorize(Roles = "Admin,SuperAdmin,StockStaff,ProductStaff,SalesStaff")]
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
            var categories = await _context.Category.Where(c => c.RecordStatus == RecordStatus.Active).OrderByDescending(c => c.Id).ToListAsync();
            var brands = await _context.Brand.Where(b => b.RecordStatus == RecordStatus.Active).OrderByDescending(b => b.Id).ToListAsync();

            // Determine what columns this role can update
            var isStockStaff   = User.IsInRole("StockStaff");
            var isProductStaff = User.IsInRole("ProductStaff");
            var isSalesStaff   = User.IsInRole("SalesStaff");
            var isAdminOrSuper = User.IsInRole("Admin") || User.IsInRole("SuperAdmin");

            // StockStaff: only stock is updatable
            // ProductStaff: name, category, brand updatable — NOT price, NOT stock
            // SalesStaff: read-only view
            // Admin/SuperAdmin: all updatable
            bool stockUpdatable    = isAdminOrSuper || isStockStaff;
            bool productUpdatable  = isAdminOrSuper || isProductStaff;
            bool priceUpdatable    = isAdminOrSuper || isSalesStaff;

            var columns = new List<object>
            {
                new { id = "image", updatable = false },
                new { 
                    id = "name", 
                    updatable = productUpdatable, 
                    validation = validationRules.GetValueOrDefault("name")
                },
                new { 
                    id = "categoryId", 
                    updatable = productUpdatable, 
                    type = "select", 
                    options = categories.Select(c => new { value = c.Id, label = c.Name }),
                    validation = validationRules.GetValueOrDefault("categoryid")
                },
                new { 
                    id = "brandId", 
                    updatable = productUpdatable, 
                    type = "select", 
                    options = brands.Select(b => new { value = b.Id, label = b.Name }),
                    validation = validationRules.GetValueOrDefault("brandid")
                },
                new { 
                    id = "price", 
                    updatable = priceUpdatable,
                    isNumeric = true,
                    validation = validationRules.GetValueOrDefault("price")
                },
                new { 
                    id = "stock", 
                    updatable = stockUpdatable,
                    isNumeric = true,
                    validation = validationRules.GetValueOrDefault("stock")
                },
                new { id = "status", updatable = false },
                new { id = "actions", updatable = false }
            };

            var rows = new Dictionary<string, object>();
            var currentUserId = _userManager.GetUserId(User);

            foreach (var p in products)
            {
                var imageUrl = p.ProductImages.OrderBy(img => img.SortOrder).FirstOrDefault()?.Url;
                
                // Calculate logs for this specific product
                var searchPattern = $"\"productId\":{p.Id}";
                var totalLogCount = await _context.ActivityLog
                    .Where(l => l.Details != null && l.Details.Contains(searchPattern))
                    .CountAsync();
                var unseenCount = await _context.ActivityLog
                    .Where(l => l.Details != null && l.Details.Contains(searchPattern))
                    .CountAsync(l => !_context.ActivityLogSeenStatus.Any(s => s.ActivityLogId == l.Id && s.AdminUserId == currentUserId));

                rows[p.Id.ToString()] = new {
                    id = p.Id,
                    image = new { image = imageUrl, isBanner = p.IsBanner },
                    name = p.Name,
                    categoryId = p.CategoryId,
                    categoryLabel = p.Category?.Name ?? "Uncategorized",
                    brandId = p.BrandId,
                    brandLabel = p.Brand?.Name ?? "No Brand",
                    price = p.Price,
                    stock = p.Stock,
                    status = p.Stock > 0 ? "Available" : "Sold Out",
                    recordStatus = p.RecordStatus.ToString(),
                    logCount = totalLogCount,
                    unseenCount = unseenCount,
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
        [Authorize(Roles = "Admin,SuperAdmin,StockStaff,ProductStaff,SalesStaff")]
        public async Task<IActionResult> UpdateTableData([FromBody] UpdateTableRequest request)
        {
            if (request == null || request.Changes == null) return BadRequest();

            var logDetails = new List<object>();
            foreach(var rowChange in request.Changes)
            {
                if (int.TryParse(rowChange.Key, out int id))
                {
                    var product = await _context.Product.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
                    if (product != null)
                    {
                        var previousValues = new Dictionary<string, object>();
                        var newValues = new Dictionary<string, object>();

                        var dbProduct = await _context.Product.Include(p => p.ProductImages).FirstOrDefaultAsync(p => p.Id == id);
                        if (dbProduct != null)
                        {
                            foreach(var colChange in rowChange.Value)
                            {
                                var colName = colChange.Key.ToLower();
                                var valueStr = colChange.Value?.ToString();

                                bool canEditProductData = User.IsInRole("Admin") || User.IsInRole("SuperAdmin") || User.IsInRole("ProductStaff");
                                bool canEditStock       = User.IsInRole("Admin") || User.IsInRole("SuperAdmin") || User.IsInRole("StockStaff");
                                bool canEditPrice       = User.IsInRole("Admin") || User.IsInRole("SuperAdmin") || User.IsInRole("SalesStaff");

                                if (canEditProductData)
                                {
                                    if (colName == "category" && int.TryParse(valueStr, out int catId) && dbProduct.CategoryId != catId)
                                    {
                                        previousValues["CategoryId"] = dbProduct.CategoryId;
                                        dbProduct.CategoryId = catId;
                                        newValues["CategoryId"] = catId;
                                    }
                                    else if (colName == "brand" && int.TryParse(valueStr, out int brId) && dbProduct.BrandId != brId)
                                    {
                                        previousValues["BrandId"] = dbProduct.BrandId;
                                        dbProduct.BrandId = brId;
                                        newValues["BrandId"] = brId;
                                    }
                                    else if (colName == "name" && dbProduct.Name != valueStr)
                                    {
                                        previousValues["Name"] = dbProduct.Name;
                                        dbProduct.Name = valueStr ?? dbProduct.Name;
                                        newValues["Name"] = dbProduct.Name;
                                    }
                                    else if (colName == "gallery" && !string.IsNullOrEmpty(valueStr))
                                    {
                                        try {
                                            using var doc = JsonDocument.Parse(valueStr);
                                            var root = doc.RootElement;
                                            
                                            bool changed = false;
                                            
                                            // Handle Deletions
                                            if (root.TryGetProperty("deleted", out var deletedProp) && deletedProp.ValueKind == JsonValueKind.Array) {
                                                foreach (var del in deletedProp.EnumerateArray()) {
                                                    var url = del.GetString();
                                                    var toDelete = dbProduct.ProductImages.FirstOrDefault(p => p.Url == url);
                                                    if (toDelete != null) {
                                                        _context.ProductImage.Remove(toDelete);
                                                        changed = true;
                                                    }
                                                }
                                            }

                                            // Handle Order
                                            if (root.TryGetProperty("order", out var orderProp)) {
                                                var orderStr = orderProp.GetString();
                                                if (!string.IsNullOrEmpty(orderStr)) {
                                                    var orderArray = orderStr.Split(',', StringSplitOptions.RemoveEmptyEntries);
                                                    for (int i = 0; i < orderArray.Length; i++) {
                                                        var img = dbProduct.ProductImages.FirstOrDefault(p => p.Url == orderArray[i]);
                                                        if (img != null && img.SortOrder != i) {
                                                            img.SortOrder = i;
                                                            changed = true;
                                                        }
                                                    }
                                                }
                                            }

                                            // Handle Additions (URLs)
                                            if (root.TryGetProperty("added", out var addedProp) && addedProp.ValueKind == JsonValueKind.Array) {
                                                foreach (var add in addedProp.EnumerateArray()) {
                                                    var url = add.GetString();
                                                    if (!string.IsNullOrWhiteSpace(url) && !dbProduct.ProductImages.Any(p => p.Url == url)) {
                                                        dbProduct.ProductImages.Add(new ProductImage { Url = url });
                                                        changed = true;
                                                    }
                                                }
                                            }

                                            if (changed) {
                                                previousValues["Gallery"] = JsonSerializer.Serialize(product.ProductImages.Select(img => new { url = img.Url, sortOrder = img.SortOrder }));
                                                newValues["Gallery"] = JsonSerializer.Serialize(dbProduct.ProductImages.Select(img => new { url = img.Url, sortOrder = img.SortOrder }));
                                            }
                                        } catch {
                                            // Fallback or ignore malformed JSON
                                        }
                                    }
                                }
                                if (canEditPrice && colName == "price" && double.TryParse(valueStr, out double price) && dbProduct.Price != price)
                                {
                                    previousValues["Price"] = dbProduct.Price;
                                    dbProduct.Price = price;
                                    newValues["Price"] = price;
                                }
                                if (canEditStock && colName == "stock" && int.TryParse(valueStr, out int stock) && dbProduct.Stock != stock)
                                {
                                    previousValues["Stock"] = dbProduct.Stock;
                                    dbProduct.Stock = stock;
                                    newValues["Stock"] = stock;
                                }
                            }
                            
                            if (newValues.Count > 0)
                            {
                                logDetails.Add(new {
                                    productId = id,
                                    table = "Product",
                                    id = id,
                                    type = "UPDATE",
                                    summary = $"Edited product <a href='/products/details/{id}' class='product-link'>{product.Name}</a> in the main table.",
                                    previousValues = previousValues,
                                    newValues = newValues
                                });

                                if (!TryValidateModel(dbProduct))
                                {
                                    return BadRequest(ModelState);
                                }
                            }
                        }
                    }
                }
            }
            await _context.SaveChangesAsync();
            
            var (cId, cName, cRole, cFullName) = await CurrentEmployeeInfoAsync();
            if (logDetails.Count > 0)
            {
                var jsonLogs = System.Text.Json.JsonSerializer.Serialize(logDetails);
                await _logger.LogAsync(ActivityAction.UpdateTable, jsonLogs, cId, cName, cRole, cFullName);
            }

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

            ViewBag.Categories = await _context.Category.Where(c => c.RecordStatus == RecordStatus.Active).OrderByDescending(c => c.Id).ToListAsync();
            ViewBag.Brands = await _context.Brand.Where(b => b.RecordStatus == RecordStatus.Active).OrderByDescending(b => b.Id).ToListAsync();

            product.ProductImages = product.ProductImages.OrderBy(p => p.SortOrder).ToList();

            return View(product);
        }

        [HttpPost]
        [Authorize(Roles = "Admin,SuperAdmin,ProductStaff,SalesStaff")]
        public async Task<IActionResult> UpdateDetails(int id, [Bind("Id,Name,CategoryId,BrandId,Price,Stock,Description,IsBanner,BannerDescription,BannerImageUrl")] Product product, List<IFormFile>? productImages, IFormFile? bannerImage, List<string>? existingImages, List<string>? imageUrls, string? deletedImages, string? imageOrder)
        {
            if (id != product.Id) return Json(new { success = false, message = "Id mismatch" });

            if (ModelState.IsValid)
            {
                try
                {
                    var deletedFiles = !string.IsNullOrEmpty(deletedImages) ? System.Text.Json.JsonSerializer.Deserialize<List<string>>(deletedImages) : new List<string>();
                    product.ProductImages.Clear();
                    if (existingImages != null) 
                    {
                        foreach (var url in existingImages) 
                        {
                            if (!string.IsNullOrWhiteSpace(url) && (deletedFiles == null || !deletedFiles.Contains(url))) 
                                product.ProductImages.Add(new ProductImage { Url = url });
                        }
                    }
                    if (productImages != null) foreach (var file in productImages) { var filePath = await SaveFile(file); product.ProductImages.Add(new ProductImage { Url = filePath }); }
                    if (imageUrls != null) foreach (var url in imageUrls) if (!string.IsNullOrWhiteSpace(url)) product.ProductImages.Add(new ProductImage { Url = url });
                    if (bannerImage != null) product.BannerImageUrl = await SaveFile(bannerImage);

                    var existingProduct = await _context.Product.Include(p => p.ProductImages).FirstOrDefaultAsync(p => p.Id == id);
                    if (existingProduct != null)
                    {
                        var previousValues = new Dictionary<string, object>
                        {
                            ["Name"] = existingProduct.Name,
                            ["CategoryId"] = existingProduct.CategoryId,
                            ["BrandId"] = existingProduct.BrandId,
                            ["Price"] = existingProduct.Price,
                            ["Stock"] = existingProduct.Stock,
                            ["Description"] = existingProduct.Description ?? "",
                            ["IsBanner"] = existingProduct.IsBanner,
                            ["BannerDescription"] = existingProduct.BannerDescription ?? "",
                            ["BannerImageUrl"] = existingProduct.BannerImageUrl ?? "",
                            ["Gallery"] = existingProduct.ProductImages.OrderBy(img => img.SortOrder).Select(img => img.Url).ToList()
                        };

                        _context.Entry(existingProduct).CurrentValues.SetValues(product);
                        _context.ProductImage.RemoveRange(existingProduct.ProductImages);
                        existingProduct.ProductImages.Clear();

                        foreach (var img in product.ProductImages) existingProduct.ProductImages.Add(img);


                        if (!string.IsNullOrEmpty(imageOrder))
                        {
                            var orderArray = imageOrder.Split('|', StringSplitOptions.RemoveEmptyEntries);
                            for (int i = 0; i < orderArray.Length; i++)
                            {
                                var img = existingProduct.ProductImages.FirstOrDefault(p => p.Url == orderArray[i]);
                                if (img != null) img.SortOrder = i;
                            }
                        }

                        existingProduct.DateModified = DateTime.UtcNow;
                        
                        var newValues = new Dictionary<string, object>
                        {
                            ["Name"] = existingProduct.Name,
                            ["CategoryId"] = existingProduct.CategoryId,
                            ["BrandId"] = existingProduct.BrandId,
                            ["Price"] = existingProduct.Price,
                            ["Stock"] = existingProduct.Stock,
                            ["Description"] = existingProduct.Description ?? "",
                            ["IsBanner"] = existingProduct.IsBanner,
                            ["BannerDescription"] = existingProduct.BannerDescription ?? "",
                            ["BannerImageUrl"] = existingProduct.BannerImageUrl ?? ""
                        };

                        var finalPrevious = previousValues.Where(kvp => kvp.Key != "Gallery" && !Equals(kvp.Value, newValues[kvp.Key])).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                        var finalNew = newValues.Where(kvp => finalPrevious.ContainsKey(kvp.Key)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                        
                        var oldGallery = previousValues["Gallery"] as List<string>;
                        var newGallery = existingProduct.ProductImages.OrderBy(img => img.SortOrder).Select(img => img.Url).ToList();
                        
                        if (!Enumerable.SequenceEqual(oldGallery, newGallery))
                        {
                            finalPrevious["Gallery"] = JsonSerializer.Serialize(previousValues["Gallery"]);
                            finalNew["Gallery"] = JsonSerializer.Serialize(newGallery);
                        }

                        await _context.SaveChangesAsync();
                        var (cId, cName, cRole, cFull) = await CurrentEmployeeInfoAsync();
                        
                        if (finalNew.Count > 0)
                        {
                            var logItem = new {
                                productId = id,
                                table = "Product",
                                id = id,
                                type = "UPDATE_INPLACE",
                                summary = $"Edited product <a href='/products/details/{id}' class='product-link'>{existingProduct.Name}</a>.",
                                previousValues = finalPrevious,
                                newValues = finalNew
                            };
                            
                            var jsonLog = JsonSerializer.Serialize(new List<object> { logItem });
                            await _logger.LogAsync(ActivityAction.EditProduct, jsonLog, cId, cName, cRole, cFull);
                        }

                        return Json(new { success = true, message = "Product updated successfully." });
                    }
                    return Json(new { success = false, message = "Product not found." });
                }
                catch (Exception ex)
                {
                    string inner = ex.InnerException != null ? " | Inner: " + ex.InnerException.Message : "";
                    return Json(new { success = false, message = "Server Error: " + ex.Message + inner });
                }
            }
            var rawForm = " | Raw Form: " + string.Join(", ", Request.Form.Keys.Take(10));
            var errors = ModelState.Where(x => x.Value.Errors.Count > 0)
                                   .Select(x => $"{x.Key}: {string.Join(", ", x.Value.Errors.Select(e => e.ErrorMessage + (e.Exception != null ? " (" + e.Exception.Message + ")" : ""))) }")
                                   .ToList();
            var errorMessage = "Invalid model state. Errors: " + string.Join(" | ", errors) + rawForm;
            return Json(new { success = false, message = errorMessage, errors = errors });
        }

        // GET: /products/create
        [Authorize(Roles = "Admin,SuperAdmin,ProductStaff")]
        public async Task<IActionResult> Create()
        {
            ViewBag.Categories = await _context.Category.Where(c => c.RecordStatus == RecordStatus.Active).OrderByDescending(c => c.Id).ToListAsync();
            ViewBag.Brands = await _context.Brand.Where(b => b.RecordStatus == RecordStatus.Active).OrderByDescending(b => b.Id).ToListAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,SuperAdmin,ProductStaff")]
        public async Task<IActionResult> Create([Bind("Id,Name,CategoryId,BrandId,Price,Stock,Description,IsBanner,BannerDescription,BannerImageUrl")] Product product, List<IFormFile>? productImages, IFormFile? bannerImage, List<string>? imageUrls, string? imageOrder)
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

                if (bannerImage != null) product.BannerImageUrl = await SaveFile(bannerImage);


                if (!string.IsNullOrEmpty(imageOrder))
                {
                    var orderArray = imageOrder.Split('|', StringSplitOptions.RemoveEmptyEntries);
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
                var logItem = new {
                    productId = product.Id,
                    table = "Product",
                    id = product.Id,
                    type = "CREATE",
                    summary = $"Created product <a href='/products/details/{product.Id}' class='product-link'>{product.Name}</a>."
                };
                var jsonLog = JsonSerializer.Serialize(new List<object> { logItem });
                    await _logger.LogAsync(ActivityAction.CreateProduct, jsonLog, cId, cName, cRole, cFull);

                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    {
                        return Json(new { success = true, message = $"Product '{product.Name}' created successfully.", id = product.Id });
                    }

                    TempData["Success"] = $"Product '{product.Name}' created successfully.";
                    return RedirectToAction(nameof(Index));
                }

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    var errors = ModelState.Where(x => x.Value.Errors.Count > 0)
                                           .Select(x => $"{x.Key}: {string.Join(", ", x.Value.Errors.Select(e => e.ErrorMessage))}")
                                           .ToList();
                    return Json(new { success = false, message = "Validation failed", errors = errors });
                }

                ViewBag.Categories = await _context.Category.Where(c => c.RecordStatus == RecordStatus.Active).OrderByDescending(c => c.Id).ToListAsync();
                ViewBag.Brands = await _context.Brand.Where(b => b.RecordStatus == RecordStatus.Active).OrderByDescending(b => b.Id).ToListAsync();
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
        [Authorize(Roles = "Admin,SuperAdmin")]
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
        [Authorize(Roles = "Admin,SuperAdmin")]
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
