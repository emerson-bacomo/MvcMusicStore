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
    [Authorize(Roles = "Admin,SuperAdmin,Staff")]
    public class CategoriesController : Controller
    {
        private readonly MvcMusicContext _context;
        private readonly IActivityLogService _logger;
        private readonly UserManager<ApplicationUser> _userManager;

        public CategoriesController(MvcMusicContext context, IActivityLogService logger, UserManager<ApplicationUser> userManager)
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
        public async Task<IActionResult> GetTableData()
        {
            var categories = await _context.Category
                .Select(c => new {
                    id = c.Id,
                    name = c.Name,
                    productCount = _context.Product.Count(p => p.CategoryId == c.Id),
                    recordStatus = c.RecordStatus.ToString()
                })
                .ToListAsync();

            var columns = new List<object>
            {
                new { id = "id", hidden = true },
                new { id = "name", updatable = true, widthPercentage = "25%" },
                new { id = "productCount", updatable = false, label = "Products" },
                new { id = "actions", updatable = false }
            };

            var rows = new Dictionary<string, object>();
            foreach (var c in categories)
            {
                rows[c.id.ToString()] = c;
            }

            return Json(new {
                columns = columns,
                rows = rows,
                updateRequest = Url.Action("UpdateTableData")
            });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateTableData([FromBody] UpdateTableRequest request)
        {
            if (request == null || request.Changes == null) return BadRequest();

            foreach(var rowChange in request.Changes)
            {
                if (int.TryParse(rowChange.Key, out int id))
                {
                    var category = await _context.Category.FindAsync(id);
                    if (category != null)
                    {
                        foreach(var colChange in rowChange.Value)
                        {
                            var colName = colChange.Key.ToLower();
                            var valueStr = colChange.Value?.ToString();

                            if (colName == "name") category.Name = valueStr ?? category.Name;
                            else if (colName == "recordstatus" && Enum.TryParse(valueStr, out RecordStatus status)) category.RecordStatus = status;
                        }
                    }
                }
            }
            await _context.SaveChangesAsync();
            
            var (cId, cName, cRole, cFullName) = await CurrentEmployeeInfoAsync();
            await _logger.LogAsync(ActivityAction.UpdateTable, $"Performed mass update on {request.Changes.Count} categories.", cId, cName, cRole, cFullName);

            return Json(new { success = true });
        }

        public async Task<IActionResult> Create(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return BadRequest("Name is required.");

            var category = new Category { Name = name };
            _context.Category.Add(category);
            await _context.SaveChangesAsync();

            var (cId, cName, cRole, cFull) = await CurrentEmployeeInfoAsync();
            await _logger.LogAsync(ActivityAction.UpdateTable, $"Created new category: {name}", cId, cName, cRole, cFull);

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var category = await _context.Category.FindAsync(id);
            if (category != null)
            {
                category.RecordStatus = RecordStatus.Deleted;
                await _context.SaveChangesAsync();
                
                var (cId, cName, cRole, cFull) = await CurrentEmployeeInfoAsync();
                await _logger.LogAsync(ActivityAction.UpdateTable, $"Soft-deleted category: {category.Name}", cId, cName, cRole, cFull);

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return Json(new { success = true });
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
