using Microsoft.AspNetCore.Identity;

namespace MvcMusic.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }

        public string FullName => $"{FirstName} {LastName}";

        /// <summary>Path to profile picture under /uploads/avatars/</summary>
        public string? ProfilePicture { get; set; }

        public bool IsBanned { get; set; } = false;
        public bool RequiresPasswordChange { get; set; } = false;

        public DateTime DateCreated { get; set; } = DateTime.UtcNow;
        public RecordStatus RecordStatus { get; set; } = RecordStatus.Active;
    }
}
