using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MvcMusic.Models
{
    public class Product
    {
        public int Id { get; set; }

        [StringLength(50, MinimumLength = 3)]
        [RegularExpression(@"^[A-Z]+[a-zA-Z\s]*$")]
        public required string Name { get; set; }

        [RegularExpression(@"^[A-Z]+[a-zA-Z\s]*$")]
        [StringLength(30)]
        public required string Category { get; set; }

        [RegularExpression(@"^[A-Z]+[a-zA-Z\s]*$")]
        public required string Brand { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        [Range(0, double.MaxValue, ErrorMessage = "Value cannot be negative.")]
        [DataType(DataType.Currency)]
        public double Price { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Value cannot be negative.")]
        public int Stock { get; set; }

        [StringLength(1000)]
        public string? Description { get; set; }
        public string? Image { get; set; } = "https://placehold.co/600x400?text=No+Image";
        public bool IsBanner { get; set; }
        public string? BannerDescription { get; set; }

        [Range(0, 5)]
        public double Rating { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Value cannot be negative.")]
        public int SoldAmount { get; set; }
    }
}