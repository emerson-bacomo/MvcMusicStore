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
    [Authorize(Roles = "Admin,SuperAdmin,ProductStaff")]
    public class BrandsController : Controller
    {
        private readonly MvcMusicContext _context;
        private readonly IActivityLogService _logger;
        private readonly UserManager<ApplicationUser> _userManager;

        public BrandsController(MvcMusicContext context, IActivityLogService logger, UserManager<ApplicationUser> userManager)
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

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetTableData(string? includeIds = null)
        {
            var brands = await _context.Brand
                .Select(b => new {
                    id = b.Id,
                    name = b.Name,
                    productCount = _context.Product.Count(p => p.BrandId == b.Id),
                    recordStatus = b.RecordStatus.ToString()
                })
                .ToListAsync();

            var columns = new List<object>
            {
                new { id = "name", updatable = true },
                new { id = "productCount", updatable = false },
                new { id = "actions", updatable = false }
            };

            var rows = new Dictionary<string, object>();
            foreach (var b in brands)
            {
                rows[b.id.ToString()] = b;
            }

            return Json(new {
                columns = columns,
                rows = rows,
                updateRequest = Url.Action("UpdateTableData")
            });
        }

        [HttpPost]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> UpdateTableData([FromBody] UpdateTableRequest request)
        {
            if (request == null || request.Changes == null) return BadRequest();

            var logDetails = new List<object>();
            foreach(var rowChange in request.Changes)
            {
                if (int.TryParse(rowChange.Key, out int id))
                {
                    var brand = await _context.Brand.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id);
                    if (brand != null)
                    {
                        var previousValues = new Dictionary<string, object>();
                        var newValues = new Dictionary<string, object>();

                        var dbBrand = await _context.Brand.FindAsync(id);
                        if (dbBrand != null)
                        {
                            foreach(var colChange in rowChange.Value)
                            {
                                var colName = colChange.Key.ToLower();
                                var valueStr = colChange.Value?.ToString();

                                if (colName == "name")
                                {
                                    if (dbBrand.Name != valueStr)
                                    {
                                        previousValues["Name"] = dbBrand.Name;
                                        dbBrand.Name = valueStr ?? dbBrand.Name;
                                        newValues["Name"] = dbBrand.Name;
                                    }
                                }
                                else if (colName == "recordstatus" && Enum.TryParse(valueStr, out RecordStatus status))
                                {
                                    if (dbBrand.RecordStatus != status)
                                    {
                                        previousValues["RecordStatus"] = dbBrand.RecordStatus.ToString();
                                        dbBrand.RecordStatus = status;
                                        newValues["RecordStatus"] = dbBrand.RecordStatus.ToString();
                                    }
                                }
                            }
                            
                            if (newValues.Count > 0)
                            {
                                logDetails.Add(new {
                                    table = "Brand",
                                    id = id,
                                    type = "UPDATE",
                                    previousValues = previousValues,
                                    newValues = newValues
                                });
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> Create(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return BadRequest("Name is required.");

            var brand = new Brand { Name = name };
            _context.Brand.Add(brand);
            await _context.SaveChangesAsync();

            var (cId, cName, cRole, cFull) = await CurrentEmployeeInfoAsync();
            await _logger.LogAsync(ActivityAction.UpdateTable, $"Created new brand: <a href='/products?brandId={brand.Id}' class='brand-link'>{name}</a>", cId, cName, cRole, cFull);

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> Delete(int id)
        {
            var brand = await _context.Brand.FindAsync(id);
            if (brand != null)
            {
                brand.RecordStatus = RecordStatus.Deleted;
                await _context.SaveChangesAsync();
                
                var (cId, cName, cRole, cFull) = await CurrentEmployeeInfoAsync();
                await _logger.LogAsync(ActivityAction.UpdateTable, $"Soft-deleted brand: <a href='/products?brandId={brand.Id}' class='brand-link'>{brand.Name}</a>", cId, cName, cRole, cFull);

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return Json(new { success = true });
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
