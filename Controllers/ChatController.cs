using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MvcMusic.Data;
using MvcMusic.Models;

namespace MvcMusic.Controllers
{
    [Authorize(Roles = "Customer")]
    public class ChatController : Controller
    {
        private readonly MvcMusicContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHubContext<MvcMusic.Hubs.ChatHub> _hubContext;

        public ChatController(MvcMusicContext context, UserManager<ApplicationUser> userManager, IHubContext<MvcMusic.Hubs.ChatHub> hubContext)
        {
            _context = context;
            _userManager = userManager;
            _hubContext = hubContext;
        }

        public IActionResult Index()
        {
            var model = new ChatSystemViewModel
            {
                IsAdmin = false
            };
            return View(model);
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StartRoomWithMessage(string subject, string content)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var room = new ChatRoom
                {
                    CustomerId = user.Id,
                    Subject = subject,
                    CreatedAt = DateTime.UtcNow
                };

                _context.ChatRoom.Add(room);
                await _context.SaveChangesAsync();

                var message = new ChatMessage
                {
                    RoomId = room.Id,
                    SenderId = user.Id,
                    Content = content,
                    Timestamp = DateTime.UtcNow
                };

                _context.ChatMessage.Add(message);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                // Notify live clients via SignalR
                var sender = await _userManager.FindByIdAsync(user.Id);
                await _hubContext.Clients.Group(room.Id.ToString()).SendAsync("ReceiveMessage", new
                {
                    id = message.Id,
                    roomId = room.Id,
                    senderId = user.Id,
                    senderName = sender?.FullName ?? "You",
                    content = content,
                    timestamp = message.Timestamp,
                    isStaff = false,
                    isRead = false
                });

                // Notify support staff to update their inbox
                await _hubContext.Clients.Group("SupportStaff").SendAsync("UpdateInbox", new {
                    roomId = room.Id,
                    content = content,
                    timestamp = message.Timestamp,
                    customerName = sender?.FullName ?? "New Customer",
                    customerId = user.Id,
                    subject = room.Subject,
                    isRead = false
                });

                return Json(new { success = true, roomId = room.Id });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ArchiveRoom(int roomId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var room = await _context.ChatRoom.FindAsync(roomId);
            if (room == null || room.CustomerId != user.Id) return Forbid();

            room.IsArchived = true;
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnarchiveRoom(int roomId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var room = await _context.ChatRoom.FindAsync(roomId);
            if (room == null || room.CustomerId != user.Id) return Forbid();

            room.IsArchived = false;
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteRoom(int roomId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var room = await _context.ChatRoom.FindAsync(roomId);
            if (room == null || room.CustomerId != user.Id) return Forbid();

            room.IsDeleted = true;
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpGet]
        public async Task<IActionResult> GetMyRooms(bool archived = false)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var rooms = await _context.ChatRoom
                .Where(r => r.CustomerId == user.Id && !r.IsDeleted && r.IsArchived == archived)
                .OrderByDescending(r => r.Messages.Any() ? r.Messages.Max(m => m.Timestamp) : r.CreatedAt)
                .Select(r => new {
                    id = r.Id,
                    subject = r.Subject,
                    createdAt = r.CreatedAt,
                    isArchived = r.IsArchived,
                    unreadCount = r.Messages.Count(m => !m.IsRead && m.SenderId != user.Id),
                    messages = r.Messages.OrderByDescending(m => m.Timestamp).Take(1).Select(m => new { content = m.Content }).ToList()
                })
                .ToListAsync();

            return Json(rooms);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsRead(int roomId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var unreadMessages = await _context.ChatMessage
                .Where(m => m.RoomId == roomId && !m.IsRead && m.SenderId != user.Id)
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
