using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MvcMusic.Data;
using MvcMusic.Models;

namespace MvcMusic.Controllers
{
    [Authorize(Roles = "Admin,SuperAdmin,CustomerStaff")]
    public class SupportController : Controller
    {
        private readonly MvcMusicContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public SupportController(MvcMusicContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: /Support
        public async Task<IActionResult> Index()
        {
            var rooms = await _context.ChatRoom
                .Include(r => r.Customer)
                .Include(r => r.Messages)
                .OrderByDescending(r => r.Messages.Any() ? r.Messages.Max(m => m.Timestamp) : r.CreatedAt)
                .ToListAsync();

            return View(rooms);
        }

        // GET: /Support/GetMessages/5
        [HttpGet]
        public async Task<IActionResult> GetMessages(int roomId)
        {
            var messages = await _context.ChatMessage
                .Where(m => m.RoomId == roomId)
                .Include(m => m.Sender)
                .OrderBy(m => m.Timestamp)
                .Select(m => new {
                    id = m.Id,
                    content = m.Content,
                    timestamp = m.Timestamp,
                    senderId = m.SenderId,
                    senderName = m.Sender != null ? m.Sender.FullName : "Unknown",
                    isStaff = _context.UserRoles.Any(ur => ur.UserId == m.SenderId) // Simple heuristic
                })
                .ToListAsync();

            return Json(messages);
        }
    }
}
