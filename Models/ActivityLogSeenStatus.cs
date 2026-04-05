using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MvcMusic.Models
{
    public class ActivityLogSeenStatus
    {
        public int Id { get; set; }

        [Required]
        public int ActivityLogId { get; set; }

        [ForeignKey("ActivityLogId")]
        public virtual ActivityLog? ActivityLog { get; set; }

        [Required]
        public string? AdminUserId { get; set; }

        [ForeignKey("AdminUserId")]
        public virtual ApplicationUser? Admin { get; set; }

        public DateTime SeenAt { get; set; } = DateTime.UtcNow;
    }
}
