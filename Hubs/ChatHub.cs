using Microsoft.AspNetCore.SignalR;
using MvcMusic.Data;
using MvcMusic.Models;
using Microsoft.EntityFrameworkCore;

namespace MvcMusic.Hubs
{
    public class ChatHub : Hub
    {
        private readonly MvcMusicContext _context;

        public ChatHub(MvcMusicContext context)
        {
            _context = context;
        }

        public async Task JoinRoom(int roomId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, roomId.ToString());
        }

        public async Task JoinSupportGroup()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "SupportStaff");
        }

        public async Task JoinPersonalGroup()
        {
            var userId = Context.UserIdentifier;
            if (!string.IsNullOrEmpty(userId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"User_{userId}");
            }
        }

        public async Task SendMessage(int roomId, string content)
        {
            var senderId = Context.UserIdentifier;
            if (string.IsNullOrEmpty(senderId)) return;

            var message = new ChatMessage
            {
                RoomId = roomId,
                SenderId = senderId,
                Content = content,
                Timestamp = DateTime.UtcNow
            };

            _context.ChatMessage.Add(message);
            await _context.SaveChangesAsync();

            // Load sender info for UI
            var sender = await _context.Users.FindAsync(senderId);
            var isStaff = await _context.Users.Where(u => u.Id == senderId).AnyAsync(); // Simple check for now, can be refined

            // We broadast to the group. The client-side will handle visibility logic.
            // But to be secure, we should ideally broadcast different payloads.
            // For now, we'll send the sender info and let the client hide it for customers if it's staff.
            
            await Clients.Group(roomId.ToString()).SendAsync("ReceiveMessage", new
            {
                id = message.Id,
                roomId = message.RoomId,
                senderId = message.SenderId,
                senderName = sender?.FullName ?? sender?.UserName,
                content = message.Content,
                timestamp = message.Timestamp,
                isStaff = Context.User.IsInRole("Admin") || Context.User.IsInRole("SuperAdmin") || Context.User.IsInRole("CustomerStaff") || Context.User.IsInRole("SalesStaff"),
                isRead = message.IsRead
            });

            // Also notify support staff to update their inbox
            var room = await _context.ChatRoom.FindAsync(roomId);
            var inboxPayload = new {
                roomId = message.RoomId,
                content = message.Content,
                timestamp = message.Timestamp,
                customerName = sender?.FullName ?? sender?.UserName,
                customerId = room?.CustomerId,
                subject = room?.Subject,
                isRead = message.IsRead
            };

            await Clients.Group("SupportStaff").SendAsync("UpdateInbox", inboxPayload);

            // Notify the specific customer if they are not the sender
            if (room != null && senderId != room.CustomerId)
            {
                await Clients.Group($"User_{room.CustomerId}").SendAsync("UpdateInbox", inboxPayload);
            }
        }

        public async Task SendTypingStatus(int roomId, bool isTyping)
        {
            var senderId = Context.UserIdentifier;
            if (string.IsNullOrEmpty(senderId)) return;

            var user = await _context.Users.FindAsync(senderId);
            var isStaff = Context.User.IsInRole("Admin") || Context.User.IsInRole("SuperAdmin") || Context.User.IsInRole("CustomerStaff") || Context.User.IsInRole("SalesStaff");
            var senderName = isStaff ? "Customer Support" : (user?.FullName ?? user?.UserName ?? "Someone");

            await Clients.Group(roomId.ToString()).SendAsync("ReceiveTypingStatus", new
            {
                roomId = roomId,
                senderId = senderId,
                senderName = senderName,
                isTyping = isTyping
            });
        }

        public async Task MarkAsSeen(int roomId)
        {
            await Clients.Group(roomId.ToString()).SendAsync("MessageSeen", new { roomId });
            // Also notify support if it's the customer who saw it, or vice versa
            if (Context.User.IsInRole("Admin") || Context.User.IsInRole("SuperAdmin") || Context.User.IsInRole("CustomerStaff") || Context.User.IsInRole("SalesStaff"))
            {
                // Seen by staff
            }
            else
            {
                // Seen by customer, notify staff
                await Clients.Group("SupportStaff").SendAsync("MessageSeen", new { roomId });
            }
        }
    }
}
