using System;
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
    [Authorize]
    public class ChatController : Controller
    {
        private readonly MvcMusicContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ChatController(MvcMusicContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StartChat(string subject)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var room = new ChatRoom
            {
                CustomerId = user.Id,
                Subject = subject,
                CreatedAt = DateTime.UtcNow
            };

            _context.ChatRoom.Add(room);
            await _context.SaveChangesAsync();
            
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers["Accept"].ToString().Contains("application/json"))
            {
                return Json(new { id = room.Id, subject = room.Subject });
            }

            return RedirectToAction("Contact", "Home");
        }

        [HttpGet]
        public async Task<IActionResult> GetMyRooms()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var rooms = await _context.ChatRoom
                .Where(r => r.CustomerId == user.Id)
                .OrderByDescending(r => r.Messages.Any() ? r.Messages.Max(m => m.Timestamp) : r.CreatedAt)
                .Select(r => new {
                    id = r.Id,
                    subject = r.Subject,
                    createdAt = r.CreatedAt,
                    messages = r.Messages.OrderByDescending(m => m.Timestamp).Take(1).Select(m => new { content = m.Content }).ToList()
                })
                .ToListAsync();

            return Json(rooms);
        }

        [HttpGet]
        public async Task<IActionResult> GetMessages(int roomId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var room = await _context.ChatRoom.FindAsync(roomId);
            if (room == null || room.CustomerId != user.Id) return Forbid();

            var messages = await _context.ChatMessage
                .Where(m => m.RoomId == roomId)
                .OrderBy(m => m.Timestamp)
                .Select(m => new {
                    id = m.Id,
                    content = m.Content,
                    timestamp = m.Timestamp,
                    senderId = m.SenderId,
                    // Hide staff names from customers
                    senderName = _context.UserRoles.Any(ur => ur.UserId == m.SenderId) ? "Customer Support" : "You"
                })
                .ToListAsync();

            return Json(messages);
        }
    }
}
