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
            var model = new ChatSystemViewModel
            {
                IsAdmin = true,
                ActiveRoomId = roomId,
                DraftCustomerId = customerId,
                DraftSubject = subject,
            };

            // Calculate unread counts if needed, but the component fetches via API
            // We just need to handle the draft name if provided
            if (!string.IsNullOrEmpty(customerId))
            {
                var customer = await _userManager.FindByIdAsync(customerId);
                if (customer != null)
                {
                    model.DraftCustomerName = customer.FullName ?? customer.UserName;
                }
            }

            return View(model);
        }

        // POST: /Support/CreateRoom
        [HttpPost]
        [ValidateAntiForgeryToken]
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsRead(int roomId)
        {
            var unreadMessages = await _context.ChatMessage
                .Where(m => m.RoomId == roomId && !m.IsRead)
                .ToListAsync();

            if (unreadMessages.Any())
            {
                foreach (var msg in unreadMessages)
                {
                    msg.IsRead = true;
                }
                await _context.SaveChangesAsync();
            }

            return Json(new { success = true });
        }

        [HttpGet]
        public async Task<IActionResult> GetMyRooms()
        {
            var rooms = await _context.ChatRoom
                .Include(r => r.Customer)
                .Include(r => r.Messages)
                .Where(r => !r.IsDeleted)
                .OrderByDescending(r => r.Messages.Any() ? r.Messages.Max(m => m.Timestamp) : r.CreatedAt)
                .Select(r => new {
                    id = r.Id,
                    subject = r.Subject,
                    customerName = r.Customer != null ? (r.Customer.FullName ?? r.Customer.UserName) : "User",
                    unreadCount = r.Messages.Count(m => !m.IsRead && m.SenderId == r.CustomerId),
                    lastMessage = r.Messages.OrderByDescending(m => m.Timestamp).Select(m => m.Content).FirstOrDefault() ?? "No messages yet"
                })
                .ToListAsync();

            return Json(rooms);
        }

        // GET: /Support/GetMessages?roomId=5
        [HttpGet]
        public async Task<IActionResult> GetMessages(int roomId)
        {
            var messages = await _context.ChatMessage
                .Where(m => m.RoomId == roomId)
                .OrderBy(m => m.Timestamp)
                .Select(m => new {
                    id = m.Id,
                    content = m.Content,
                    timestamp = m.Timestamp,
                    senderId = m.SenderId,
                    senderName = m.Sender != null ? (m.Sender.FullName ?? m.Sender.UserName) : "Unknown",
                    isRead = m.IsRead
                })
                .ToListAsync();

            return Json(messages);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteRoom(int roomId)
        {
            var room = await _context.ChatRoom.FindAsync(roomId);
            if (room == null) return NotFound();

            room.IsDeleted = true;
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }
    }
}
