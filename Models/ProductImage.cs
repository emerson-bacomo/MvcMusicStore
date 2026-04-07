using System.ComponentModel.DataAnnotations;

namespace MvcMusic.Models
{
    public class ProductImage
    {
        public int Id { get; set; }
        
        [Required]
        public string Url { get; set; } = string.Empty;

        public int ProductId { get; set; }
        public virtual Product? Product { get; set; }

        public int SortOrder { get; set; }
    }
}
