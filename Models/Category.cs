using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MvcMusic.Models
{
    public class Category
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;


        public RecordStatus RecordStatus { get; set; } = RecordStatus.Active;

        // Navigation property
        public virtual ICollection<Product> Products { get; set; } = new List<Product>();
    }
}
