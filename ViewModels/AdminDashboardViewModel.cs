namespace MvcMusic.ViewModels
{
    public class AdminDashboardViewModel
    {
        // Summary stats
        public decimal TotalRevenue { get; set; }
        public int TotalOrders { get; set; }
        public int TotalCustomers { get; set; }
        public int TotalProducts { get; set; }
        public int TotalEmployees { get; set; }

        // Sales chart data (label → amount)
        public List<ChartPoint> SalesByDay { get; set; } = new();      // last 30 days
        public List<ChartPoint> SalesByWeek { get; set; } = new();     // last 12 weeks
        public List<ChartPoint> SalesByMonth { get; set; } = new();    // last 12 months
        public List<ChartPoint> SalesBySeason { get; set; } = new();   // 4 seasons

        // Top sellers (product name → sold amount)
        public List<ChartPoint> TopSellers { get; set; } = new();

        // Revenue by category
        public List<ChartPoint> RevenueByCategory { get; set; } = new();

        // Recent orders
        public List<RecentOrderItem> RecentOrders { get; set; } = new();
    }

    public class ChartPoint
    {
        public string Label { get; set; } = string.Empty;
        public decimal Value { get; set; }
    }

    public class RecentOrderItem
    {
        public int Id { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string Products { get; set; } = string.Empty;
        public int TotalQuantity { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime Date { get; set; }
    }
}
