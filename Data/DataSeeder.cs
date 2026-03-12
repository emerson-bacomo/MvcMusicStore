using Microsoft.AspNetCore.Identity;
using MvcMusic.Models;
using MvcMusic.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MvcMusic.Data
{
    public static class DataSeeder
    {
        public static async Task SeedAllAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<MvcMusicContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            await SeedRolesAsync(roleManager);
            await SeedEmployeesAsync(userManager);
            var customers = await SeedCustomersAsync(userManager);
            await SeedProductsAsync(context);
            await SeedOrdersAsync(context, customers);
        }

        private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
        {
            string[] roleNames = { "SuperAdmin", "Admin", "Staff", "User" };
            foreach (var roleName in roleNames)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                    await roleManager.CreateAsync(new IdentityRole(roleName));
            }
        }

        private static async Task SeedEmployeesAsync(UserManager<ApplicationUser> userManager)
        {
            var year = DateTime.UtcNow.ToString("yy");

            // Seed SuperAdmin
            var superAdminEmail = "superadmin@nightcord.com";
            if (await userManager.FindByEmailAsync(superAdminEmail) == null)
            {
                var user = new ApplicationUser
                {
                    UserName = $"{year}-X0001",
                    Email = superAdminEmail,
                    FirstName = "Super",
                    LastName = "Admin",
                    EmailConfirmed = true,
                    DateCreated = DateTime.UtcNow
                };
                if ((await userManager.CreateAsync(user, "SuperAdmin@123")).Succeeded)
                    await userManager.AddToRoleAsync(user, "SuperAdmin");
            }

            // Seed Admin
            var adminEmail = "admin@nightcord.com";
            if (await userManager.FindByEmailAsync(adminEmail) == null)
            {
                var user = new ApplicationUser
                {
                    UserName = $"{year}-A0001",
                    Email = adminEmail,
                    FirstName = "Alice",
                    LastName = "Rivera",
                    EmailConfirmed = true,
                    DateCreated = DateTime.UtcNow
                };
                if ((await userManager.CreateAsync(user, "Admin@123")).Succeeded)
                    await userManager.AddToRoleAsync(user, "Admin");
            }

            // Seed Staff
            var staffEmail = "staff@nightcord.com";
            if (await userManager.FindByEmailAsync(staffEmail) == null)
            {
                var user = new ApplicationUser
                {
                    UserName = $"{year}-S0001",
                    Email = staffEmail,
                    FirstName = "Carlos",
                    LastName = "Mendez",
                    EmailConfirmed = true,
                    DateCreated = DateTime.UtcNow
                };
                if ((await userManager.CreateAsync(user, "Staff@123")).Succeeded)
                    await userManager.AddToRoleAsync(user, "Staff");
            }
        }

        private static async Task<List<ApplicationUser>> SeedCustomersAsync(UserManager<ApplicationUser> userManager)
        {
            var rng = new Random(42);
            var customerNames = new[] { "Maria Santos", "John Cruz", "Ana Reyes", "Miguel Torres", "Sofia Lim", "James Park", "Elena Gomez", "David Kim" };
            var customers = new List<ApplicationUser>();

            for (int i = 0; i < customerNames.Length; i++)
            {
                var nameParts = customerNames[i].Split(' ');
                var email = $"customer{i + 1}@nightcord.com";
                var user = await userManager.FindByEmailAsync(email);
                if (user == null)
                {
                    user = new ApplicationUser
                    {
                        UserName = $"customer{i + 1}",
                        Email = email,
                        FirstName = nameParts[0],
                        LastName = nameParts[1],
                        EmailConfirmed = true,
                        DateCreated = DateTime.UtcNow.AddMonths(-rng.Next(1, 12))
                    };
                    if ((await userManager.CreateAsync(user, "Customer@123")).Succeeded)
                        await userManager.AddToRoleAsync(user, "User");
                }
                customers.Add(user);
            }
            return customers;
        }

        private static async Task SeedProductsAsync(MvcMusicContext context)
        {
            if (context.Product.Any()) return;

            var products = ProductData.GetBaseProducts();
            foreach (var p in products)
            {
                p.Rating = 0;
                p.RatingsCount = 0;
                p.SoldAmount = 0;
            }

            context.Product.AddRange(products);
            await context.SaveChangesAsync();
        }

        private static async Task SeedOrdersAsync(MvcMusicContext context, List<ApplicationUser> customers)
        {
            if (context.Order.Any()) return;

            var rng = new Random(42);
            var now = DateTime.UtcNow;
            var dbProducts = await context.Product.ToListAsync();
            if (!dbProducts.Any() || !customers.Any()) return;

            var orders = new List<Order>();

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
                        Quantity = qty
                    });
                    total += (decimal)p.Price * qty;
                }
                order.TotalAmount = total;
                orders.Add(order);
            }

            context.Order.AddRange(orders);
            await context.SaveChangesAsync();

            // ── Decrement Stock for Delivered/OnDelivery orders ──────────
            foreach (var order in orders)
            {
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
            await context.SaveChangesAsync();
        }
    }
}
