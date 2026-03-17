using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MvcMusic.Data;
using MvcMusic.Models;
using MvcMusic.Utils;

namespace MvcMusic.Controllers
{
    [Route("activity-logs")]
    [Authorize(Roles = "Admin,SuperAdmin,Staff")]
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

        // GET: /activity-logs
        [Route("")]
        [Route("index")]
        public async Task<IActionResult> Index(string? userId = null, string? searchTerm = null, string? actionFilter = null, int page = 1, int pageSize = 20)
        {
            IQueryable<ActivityLog> query = _context.ActivityLog.OrderByDescending(l => l.Timestamp);

            if (!string.IsNullOrEmpty(userId))
            {
                var targetUser = await _userManager.FindByIdAsync(userId);
                ViewBag.Employee = targetUser;
                query = query.Where(l => l.UserId == userId);
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

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            page = Math.Max(1, Math.Min(totalPages > 0 ? totalPages : 1, page));

            var logs = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.UserId = userId;
            ViewBag.SearchTerm = searchTerm;
            ViewBag.ActionFilter = actionFilter;
            
            // For the filter dropdown - fetch to memory to avoid translation issue
            var allActions = await _context.ActivityLog
                .Select(l => l.Action)
                .ToListAsync();
                
            ViewBag.Actions = allActions
                .Select(a => a.ToString())
                .Distinct()
                .OrderBy(a => a)
                .ToList();

            return View(logs);
        }
    }
}
