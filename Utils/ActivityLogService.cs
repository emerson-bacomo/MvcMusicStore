using MvcMusic.Models;
using MvcMusic.Data;

namespace MvcMusic.Utils
{
    public interface IActivityLogService
    {
        Task LogAsync(string action, string? details, string? userId, string? username, string? role);
    }

    public class ActivityLogService : IActivityLogService
    {
        private readonly MvcMusicContext _context;

        public ActivityLogService(MvcMusicContext context)
        {
            _context = context;
        }

        public async Task LogAsync(string action, string? details, string? userId, string? username, string? role)
        {
            var log = new ActivityLog
            {
                Action = action,
                Details = details,
                UserId = userId,
                Username = username,
                Role = role,
                Timestamp = DateTime.UtcNow
            };
            _context.ActivityLog.Add(log);
            await _context.SaveChangesAsync();
        }
    }
}
