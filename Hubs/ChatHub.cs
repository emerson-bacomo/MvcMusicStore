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
            if (Context.User.IsInRole("Admin") || Context.User.IsInRole("SuperAdmin") || Context.User.IsInRole("Staff"))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, "SupportStaff");
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
                isStaff = Context.User.IsInRole("Admin") || Context.User.IsInRole("SuperAdmin") || Context.User.IsInRole("Staff")
            });

            // Also notify support staff to update their inbox
            await Clients.Group("SupportStaff").SendAsync("UpdateInbox", new {
                roomId = message.RoomId,
                content = message.Content,
                timestamp = message.Timestamp,
                customerName = sender?.FullName ?? sender?.UserName,
                subject = (await _context.ChatRoom.FindAsync(roomId))?.Subject
            });
        }

        public async Task SendTypingStatus(int roomId, bool isTyping)
        {
            var senderId = Context.UserIdentifier;
            if (string.IsNullOrEmpty(senderId)) return;

            var user = await _context.Users.FindAsync(senderId);
            var isStaff = Context.User.IsInRole("Admin") || Context.User.IsInRole("SuperAdmin") || Context.User.IsInRole("Staff");
            var senderName = isStaff ? "Customer Support" : (user?.FullName ?? user?.UserName ?? "Someone");

            await Clients.Group(roomId.ToString()).SendAsync("ReceiveTypingStatus", new
            {
                roomId = roomId,
                senderId = senderId,
                senderName = senderName,
                isTyping = isTyping
            });
        }
    }
}
