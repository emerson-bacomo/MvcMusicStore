using MvcMusic.Models;
using MvcMusic.Data;

namespace MvcMusic.ViewModels
{
    public class DemoViewModel
    {
        public DataSeeder.SeedOptions Options { get; set; } = new DataSeeder.SeedOptions();
        
        // Preview Data
        public List<ApplicationUser> EmployeesPreview { get; set; } = new List<ApplicationUser>();
        public List<ApplicationUser> CustomersPreview { get; set; } = new List<ApplicationUser>();
        public List<Product> ProductsPreview { get; set; } = new List<Product>();
        public List<string> CategoriesPreview { get; set; } = new List<string>();
        public List<string> BrandsPreview { get; set; } = new List<string>();
        public List<OrderPreview> OrdersPreview { get; set; } = new List<OrderPreview>();
        public ApplicationUser SuperAdminPreview { get; set; } = new ApplicationUser();
        public MvcMusic.Utils.DemoLockInfo LockInfo { get; set; } = new MvcMusic.Utils.DemoLockInfo();
    }

    public class OrderPreview
    {
        public int Id { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
