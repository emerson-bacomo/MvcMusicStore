using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MvcMusic.Models
{
    public class Order
    {
        public int Id { get; set; }

        [Required]
        public string? CustomerId { get; set; }

        public DateTime OrderDate { get; set; } = DateTime.UtcNow;

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        [Required]
        /// <summary>Pending, OnDelivery, Delivered, Cancelled</summary>
        [StringLength(30)]
        public string Status { get; set; } = "Pending";

        public string? ReceiverName { get; set; }
        public string? Address { get; set; }

        // Snapshots
        public string? Username { get; set; }
        public string? UserFullName { get; set; }

        public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    }

    public class OrderItem
    {
        public int Id { get; set; }

        public int OrderId { get; set; }
        public virtual Order Order { get; set; } = default!;

        public int ProductId { get; set; }
        public virtual Product Product { get; set; } = default!;

        [Required]
        public int Quantity { get; set; }

        // Snapshots
        public string? ProductName { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }
    }
}
