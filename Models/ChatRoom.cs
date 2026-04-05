using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MvcMusic.Models
{
    public class ChatRoom
    {
        public int Id { get; set; }

        [Required]
        public string CustomerId { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Subject { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("CustomerId")]
        public virtual ApplicationUser? Customer { get; set; }

        public virtual ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
        public bool IsDeleted { get; set; } = false;
        public bool IsArchived { get; set; } = false;
    }
}
