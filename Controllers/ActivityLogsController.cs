using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MvcMusic.Data;
using MvcMusic.Models;
using MvcMusic.Utils;
using MvcMusic.ViewModels;

namespace MvcMusic.Controllers
{
    [Route("activity-logs")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public class ActivityLogsController : Controller
    {
        private readonly MvcMusicContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IActivityLogService _activityLogger;

        public ActivityLogsController(MvcMusicContext context, UserManager<ApplicationUser> userManager, IActivityLogService activityLogger)
        {
            _context = context;
            _userManager = userManager;
            _activityLogger = activityLogger;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? userId = null, int? productId = null)
        {
            var currentAdminId = _userManager.GetUserId(User);

            if (!string.IsNullOrEmpty(userId))
            {
                var targetUser = await _userManager.FindByIdAsync(userId);
                ViewBag.Employee = targetUser;
                ViewBag.UserId = userId;
                
                // Mark current logs for this user as seen by this admin
                // Exclude Login/Logout actions to prevent cluttering the unseen count
                var unseenLogIds = await _context.ActivityLog
                    .Where(l => l.UserId == userId)
                    .Where(l => l.Action != ActivityAction.Login && l.Action != ActivityAction.Logout)
                    .Where(l => !_context.ActivityLogSeenStatus
                        .Any(s => s.ActivityLogId == l.Id && s.AdminUserId == currentAdminId))
                    .Select(l => l.Id)
                    .ToListAsync();

                if (unseenLogIds.Any())
                {
                    var newSeenStatuses = unseenLogIds.Select(id => new ActivityLogSeenStatus
                    {
                        ActivityLogId = id,
                        AdminUserId = currentAdminId
                    });
                    _context.ActivityLogSeenStatus.AddRange(newSeenStatuses);
                    await _context.SaveChangesAsync();

                    TempData["JustSeenLogIds"] = string.Join(",", unseenLogIds);
                }
            }

            if (productId.HasValue)
            {
                var product = await _context.Product.FindAsync(productId.Value);
                ViewBag.Product = product;
                
                var productIdString = productId.Value.ToString();
                var searchPattern = $"\"productId\":{productIdString}";
                var unseenLogIds = await _context.ActivityLog
                    .Where(l => l.Details != null && l.Details.Contains(searchPattern))
                    .Where(l => !_context.ActivityLogSeenStatus.Any(s => s.ActivityLogId == l.Id && s.AdminUserId == currentAdminId))
                    .Select(l => l.Id)
                    .ToListAsync();

                if (unseenLogIds.Any())
                {
                    var newSeenStatuses = unseenLogIds.Select(id => new ActivityLogSeenStatus
                    {
                        ActivityLogId = id,
                        AdminUserId = currentAdminId
                    });
                    _context.ActivityLogSeenStatus.AddRange(newSeenStatuses);
                    await _context.SaveChangesAsync();

                    var existingJustSeen = TempData["JustSeenLogIds"] as string ?? "";
                    var allJustSeen = string.IsNullOrEmpty(existingJustSeen) 
                        ? unseenLogIds 
                        : existingJustSeen.Split(',').Select(int.Parse).Concat(unseenLogIds).Distinct();
                    TempData["JustSeenLogIds"] = string.Join(",", allJustSeen);
                }
            }
            
            // For the filter dropdowns in UpdatableTable config or custom UI if needed
            var allActions = await _context.ActivityLog
                .Select(l => l.Action)
                .Distinct()
                .ToListAsync();
                
            ViewBag.Actions = allActions
                .Select(a => a.ToString())
                .OrderBy(a => a)
                .ToList();

            ViewBag.Roles = await _context.ActivityLog
                .Where(l => !string.IsNullOrEmpty(l.Role))
                .Select(l => l.Role)
                .Distinct()
                .OrderBy(r => r)
                .ToListAsync();

            ViewBag.Users = await _context.ActivityLog
                .Select(l => new { l.UserId, l.Username, l.UserFullName })
                .Distinct()
                .OrderBy(u => u.Username)
                .Select(u => new ActivityLogUserViewModel { UserId = u.UserId, Username = u.Username, UserFullName = u.UserFullName })
                .ToListAsync();

            return View();
        }

        [HttpGet("data")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> GetTableData(
            string? userId = null, 
            string? searchTerm = null, 
            string? actionFilter = null, 
            string? roleFilter = null,
            DateTime? startDate = null,
            DateTime? endDate = null,
            string? productId = null,
            int page = 1, 
            int pageSize = 20)
        {
            IQueryable<ActivityLog> query = _context.ActivityLog.OrderByDescending(l => l.Timestamp);

            if (!string.IsNullOrEmpty(userId))
            {
                query = query.Where(l => l.UserId == userId);
            }

            if (!string.IsNullOrEmpty(productId))
            {
                var searchPattern = $"\"productId\":{productId}";
                query = query.Where(l => l.Details != null && l.Details.Contains(searchPattern));
            }

            if (!string.IsNullOrEmpty(searchTerm))
            {
                var lowerSearch = searchTerm.ToLower();
                query = query.Where(l => 
                    l.Username.ToLower().Contains(lowerSearch) || 
                    l.UserFullName.ToLower().Contains(lowerSearch) || 
                    l.Action.ToString().ToLower().Contains(lowerSearch) || 
                    l.Details.ToLower().Contains(lowerSearch));
            }

            if (!string.IsNullOrEmpty(actionFilter))
            {
                if (Enum.TryParse<ActivityAction>(actionFilter, out var actionEnum))
                {
                    query = query.Where(l => l.Action == actionEnum);
                }
            }

            if (!string.IsNullOrEmpty(roleFilter))
            {
                query = query.Where(l => l.Role == roleFilter);
            }

            if (startDate.HasValue)
            {
                query = query.Where(l => l.Timestamp >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                // Set to end of day if time is not specified
                var endOfDate = endDate.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(l => l.Timestamp <= endOfDate);
            }

            var logs = await query.ToListAsync();

            var columns = new List<object>
            {
                new { id = "timestamp" },
                new { id = "username" },
                new { id = "fullname" },
                new { id = "role" },
                new { id = "action" },
                new { id = "details" }
            };

            var justSeenIdsStr = TempData["JustSeenLogIds"] as string;
            var justSeenIds = !string.IsNullOrEmpty(justSeenIdsStr) 
                ? justSeenIdsStr.Split(',').Select(int.Parse).ToHashSet() 
                : new HashSet<int>();

            var rows = logs.ToDictionary(l => l.Id, l => (object)new
            {
                id = l.Id,
                userId = l.UserId ?? "",
                timestamp = l.Timestamp.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                username = string.IsNullOrEmpty(l.Username) ? "—" : l.Username,
                fullname = string.IsNullOrEmpty(l.UserFullName) ? "—" : l.UserFullName,
                role = l.Role ?? "",
                action = l.Action.ToString(),
                details = l.Details ?? "",
                _isNew = justSeenIds.Contains(l.Id)
            });

            return Json(new 
            {
                columns = columns,
                rows = rows
            });
        }
    }
}
