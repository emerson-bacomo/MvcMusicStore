using MvcMusic.Models;
using MvcMusic.Data;

namespace MvcMusic.Utils
{
    public interface IActivityLogService
    {
        Task LogAsync(ActivityAction action, string? details, string? userId, string? username, string? role, string? fullName = null);
    }

    public class ActivityLogService : IActivityLogService
    {
        private readonly MvcMusicContext _context;

        public ActivityLogService(MvcMusicContext context)
        {
            _context = context;
        }

        public async Task LogAsync(ActivityAction action, string? details, string? userId, string? username, string? role, string? fullName = null)
        {
            var log = new ActivityLog
            {
                Action = action,
                Details = details,
                UserId = userId,
                Username = username,
                UserFullName = fullName,
                Role = role,
                Timestamp = DateTime.UtcNow
            };
            _context.ActivityLog.Add(log);
            await _context.SaveChangesAsync();
        }
    }
}
