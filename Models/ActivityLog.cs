using System;
using System.ComponentModel.DataAnnotations;

namespace MvcMusic.Models
{
    public class ActivityLog
    {
        public int Id { get; set; }

        [Required]
        public ActivityAction Action { get; set; }
 
        public string? Details { get; set; }

        public string? UserId { get; set; }

        [StringLength(256)]
        public string? Username { get; set; }

        [StringLength(256)]
        public string? UserFullName { get; set; }

        [StringLength(50)]
        public string? Role { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
