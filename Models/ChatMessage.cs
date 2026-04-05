using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MvcMusic.Models
{
    public class ChatMessage
    {
        public int Id { get; set; }

        [Required]
        public int RoomId { get; set; }

        [Required]
        public string SenderId { get; set; } = string.Empty;

        [Required]
        public string Content { get; set; } = string.Empty;

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public bool IsRead { get; set; } = false;

        [ForeignKey("RoomId")]
        public virtual ChatRoom? Room { get; set; }

        [ForeignKey("SenderId")]
        public virtual ApplicationUser? Sender { get; set; }
    }
}
