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
    [Authorize(Roles = "Admin,SuperAdmin,CustomerStaff,SalesStaff")]
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
        public async Task<IActionResult> Index(int? roomId = null, string? customerId = null, string? subject = null)
        {
            var rooms = await _context.ChatRoom
                .Where(r => !r.IsDeleted)
                .Include(r => r.Customer)
                .Include(r => r.Messages)
                .OrderByDescending(r => r.Messages.Any() ? r.Messages.Max(m => m.Timestamp) : r.CreatedAt)
                .ToListAsync();

            ViewBag.ActiveRoomId = roomId;

            // Handle Draft Mode
            if (roomId == null && !string.IsNullOrEmpty(customerId))
            {
                // Check if room actually exists
                var existing = await _context.ChatRoom.FirstOrDefaultAsync(r => r.CustomerId == customerId && r.Subject == subject && !r.IsDeleted);
                if (existing != null)
                {
                    ViewBag.ActiveRoomId = existing.Id;
                }
                else
                {
                    var customer = await _userManager.FindByIdAsync(customerId);
                    if (customer != null)
                    {
                        ViewBag.DraftCustomerId = customerId;
                        ViewBag.DraftSubject = subject ?? "Support";
                        ViewBag.DraftCustomerName = customer.FullName ?? customer.UserName;
                    }
                }
            }

            return View(rooms);
        }

        // POST: /Support/CreateRoom
        [HttpPost]
        public async Task<IActionResult> CreateRoom(string customerId, string subject)
        {
            if (string.IsNullOrEmpty(customerId)) return Json(new { success = false, message = "Customer ID is required." });

            var room = new ChatRoom
            {
                CustomerId = customerId,
                Subject = subject ?? "Support",
                CreatedAt = DateTime.UtcNow
            };

            _context.ChatRoom.Add(room);
            await _context.SaveChangesAsync();

            return Json(new { success = true, id = room.Id });
        }

        // POST: /Support/DeleteRoom
        [HttpPost]
        public async Task<IActionResult> DeleteRoom(int roomId)
        {
            var room = await _context.ChatRoom.FindAsync(roomId);
            if (room == null) return NotFound();

            room.IsDeleted = true;
            await _context.SaveChangesAsync();

            return Json(new { success = true });
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
