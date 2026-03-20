using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MvcMusic.Data;
using MvcMusic.Models;
using MvcMusic.ViewModels;

namespace MvcMusic.Controllers
{
    [Authorize(Roles = "Admin,SuperAdmin,Staff")]
    public class AdminDashboardController : Controller
    {
        private readonly MvcMusicContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public AdminDashboardController(MvcMusicContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index(int ordersCount = 10)
        {
            var user = await _userManager.GetUserAsync(User);
            ViewData["CurrentUserName"] = user?.FullName ?? user?.UserName ?? "Admin";

            var now = DateTime.UtcNow;
            var vm = new AdminDashboardViewModel();

            // ── Summary Stats (Database Aggregates) ─────────────────────
            vm.TotalRevenue = await _context.Order.Where(o => o.Status == "Delivered").SumAsync(o => o.TotalAmount);
            vm.TotalOrders = await _context.Order.CountAsync();
            vm.TotalProducts = await _context.Product.CountAsync();

            // Count Users by Role efficiently
            var userRoleId = await _context.Roles.Where(r => r.Name == "User").Select(r => r.Id).FirstOrDefaultAsync();
            vm.TotalCustomers = await _context.UserRoles.CountAsync(ur => ur.RoleId == userRoleId);

            var employeeRoleIds = await _context.Roles
                .Where(r => r.Name == "Admin" || r.Name == "Staff" || r.Name == "SuperAdmin")
                .Select(r => r.Id)
                .ToListAsync();
            vm.TotalEmployees = await _context.UserRoles.CountAsync(ur => employeeRoleIds.Contains(ur.RoleId));

            // Load Sales Data points (only required fields for charts)
            var salesData = await _context.Order
                .Select(o => new { o.OrderDate, o.TotalAmount })
                .ToListAsync();

            // ── Sales By Day (last 30 days) ─────────────────────────
            for (int d = 29; d >= 0; d--)
            {
                var day = now.Date.AddDays(-d);
                var amt = salesData.Where(o => o.OrderDate.Date == day).Sum(o => o.TotalAmount);
                vm.SalesByDay.Add(new ChartPoint { Label = day.ToString("MMM d"), Value = amt });
            }

            // ── Sales By Week (last 12 weeks) ───────────────────────
            for (int w = 11; w >= 0; w--)
            {
                var weekStart = now.Date.AddDays(-(w * 7 + (int)now.DayOfWeek));
                var weekEnd = weekStart.AddDays(7);
                var amt = salesData.Where(o => o.OrderDate.Date >= weekStart && o.OrderDate.Date < weekEnd).Sum(o => o.TotalAmount);
                vm.SalesByWeek.Add(new ChartPoint { Label = $"Wk {weekStart:MMM d}", Value = amt });
            }

            // ── Sales By Month (last 12 months) ─────────────────────
            for (int m = 11; m >= 0; m--)
            {
                var month = new DateTime(now.Year, now.Month, 1).AddMonths(-m);
                var amt = salesData.Where(o => o.OrderDate.Year == month.Year && o.OrderDate.Month == month.Month).Sum(o => o.TotalAmount);
                vm.SalesByMonth.Add(new ChartPoint { Label = month.ToString("MMM yyyy"), Value = amt });
            }

            // ── Sales By Season ─────────────────────────────────────
            var seasons = new[] { ("Winter", 12, 2), ("Spring", 3, 5), ("Summer", 6, 8), ("Autumn", 9, 11) };
            foreach (var (name, startM, endM) in seasons)
            {
                decimal amt;
                if (name == "Winter")
                    amt = salesData.Where(o => o.OrderDate.Month == 12 || o.OrderDate.Month <= 2).Sum(o => o.TotalAmount);
                else
                    amt = salesData.Where(o => o.OrderDate.Month >= startM && o.OrderDate.Month <= endM).Sum(o => o.TotalAmount);
                vm.SalesBySeason.Add(new ChartPoint { Label = name, Value = amt });
            }

            // ── Top Sellers ─────────────────────────────────────────
            vm.TopSellers = await _context.Product
                .OrderByDescending(p => p.SoldAmount)
                .Take(5)
                .Select(p => new ChartPoint { Label = p.Name.Length > 28 ? p.Name.Substring(0, 28) + "…" : p.Name, Value = (decimal)p.SoldAmount })
                .ToListAsync();

            // ── Revenue By Category ──────────────────
            vm.RevenueByCategory = await _context.Set<ChartPoint>()
                .FromSqlRaw(@"
                    SELECT 
                        c.Name AS Label,
                        SUM(p.Price * oi.Quantity) AS Value
                    FROM OrderItem oi
                    INNER JOIN Product p ON oi.ProductId = p.Id
                    INNER JOIN Category c ON p.CategoryId = c.Id
                    GROUP BY c.Id, c.Name
                    ORDER BY Value DESC
                ")
                .ToListAsync();

            return View(vm);
        }
    }
}
