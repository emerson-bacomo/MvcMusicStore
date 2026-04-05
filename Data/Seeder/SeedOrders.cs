using Microsoft.EntityFrameworkCore;
using MvcMusic.Models;
using Microsoft.Extensions.Logging;
using MvcMusic.Utils;

namespace MvcMusic.Data
{
    public static partial class DataSeeder
    {
        private static async Task SeedOrdersAsync(MvcMusicContext context, List<ApplicationUser> customers, ILogger logger, IActivityLogService activityLogger)
        {
            if (context.Order.Any())
            {
                logger.LogInformation("Orders already seeded, skipping.");
                return;
            }

            logger.LogInformation("Seeding orders...");

            var rng = new Random(42);
            var now = DateTime.UtcNow;
            var dbProducts = await context.Product.ToListAsync();
            if (!dbProducts.Any() || !customers.Any()) return;

            var orders = new List<Order>();
            
            // Use a small subset for ActivityLogs to avoid flooding
            var logCount = 0;

            // Generate ~150 orders across the past 2 years
            for (int i = 0; i < 150; i++)
            {
                var daysAgo = rng.Next(0, 730);
                var orderDate = now.AddDays(-daysAgo);
                var status = daysAgo < 5 ? "Pending" : daysAgo < 14 ? "OnDelivery" : "Delivered";
                var customer = customers[rng.Next(customers.Count)];

                var order = new Order
                {
                    CustomerId = customer.Id,
                    OrderDate = orderDate,
                    Status = status,
                    ReceiverName = customer.FullName,
                    Address = "Cebu City, Cebu, Philippines",
                    Username = customer.UserName,
                    UserFullName = customer.FullName,
                    OrderItems = new List<OrderItem>()
                };

                // Each order has 1 to 4 different products
                var itemsCount = rng.Next(1, 5);
                var selectedProducts = dbProducts.OrderBy(x => rng.Next()).Take(itemsCount).ToList();

                decimal total = 0;
                foreach (var p in selectedProducts)
                {
                    var qty = rng.Next(1, 3);
                    order.OrderItems.Add(new OrderItem
                    {
                        ProductId = p.Id,
                        Quantity = qty,
                        ProductName = p.Name,
                        Price = (decimal)p.Price
                    });
                    total += (decimal)p.Price * qty;
                }
                order.TotalAmount = total;
                orders.Add(order);
            }

            context.Order.AddRange(orders);
            await context.SaveChangesAsync();

            foreach (var order in orders)
            {
                var customer = customers.FirstOrDefault(c => c.Id == order.CustomerId);
                if (logCount < 20 && customer != null) // Log first 20 orders as activity
                {
                    await activityLogger.LogAsync(ActivityAction.PlaceOrder, $"Placed order <a href='/orders/detail/{order.Id}' class='order-link'>#{order.Id}</a> for {order.TotalAmount:C2}.", customer.Id, customer.UserName, "Customer", customer.FullName);
                    logCount++;
                }

                // ── Decrement Stock for Delivered/OnDelivery orders ──────────
                if (order.Status == "OnDelivery" || order.Status == "Delivered")
                {
                    foreach (var item in order.OrderItems)
                    {
                        var product = dbProducts.FirstOrDefault(p => p.Id == item.ProductId);
                        if (product != null)
                        {
                            product.Stock = Math.Max(0, product.Stock - item.Quantity);
                        }
                    }
                }
            }
            await context.SaveChangesAsync();

            // ── Update Product Stats based on DELIVERED seeded orders only ──
            var deliveredOrders = orders.Where(o => o.Status == "Delivered").ToList();
            var deliveredItems = deliveredOrders.SelectMany(o => o.OrderItems).ToList();

            foreach (var p in dbProducts)
            {
                var productDeliveredItems = deliveredItems.Where(i => i.ProductId == p.Id).ToList();
                p.SoldAmount = productDeliveredItems.Sum(i => i.Quantity);
                
                if (p.SoldAmount > 0)
                {
                    // Simulate that ~70% of delivered sales result in a rating
                    p.RatingsCount = (int)(p.SoldAmount * 0.7) + rng.Next(1, 5);
                    // Keep rating in the 4.9 - 5.0 range
                    p.Rating = 4.9 + (rng.NextDouble() * 0.1);
                }
                else
                {
                    p.RatingsCount = 0;
                    p.Rating = 0;
                }
            }
            logger.LogInformation("Successfully seeded {OrderCount} orders and associated data.", orders.Count);

            await context.SaveChangesAsync();
        }
    }
}
