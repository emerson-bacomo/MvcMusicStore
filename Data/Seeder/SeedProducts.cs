using Microsoft.EntityFrameworkCore;
using MvcMusic.Models;
using Microsoft.Extensions.Logging;
using MvcMusic.Utils;

namespace MvcMusic.Data
{
    public static partial class DataSeeder
    {
        private static async Task SeedProductsAsync(MvcMusicContext context, ILogger logger, IActivityLogService activityLogger, ApplicationUser? superAdmin)
        {
            if (context.Product.Any())
            {
                logger.LogInformation("Products already seeded, skipping.");
                return;
            }

            logger.LogInformation("Seeding products...");

            var baseProducts = GetBaseProducts();
            var categories = new Dictionary<string, Category>();
            var brands = new Dictionary<string, Brand>();

            foreach (var p in baseProducts)
            {
                var catName = p.Category?.Name ?? "Uncategorized";
                var brName = p.Brand?.Name ?? "No Brand";

                if (!categories.ContainsKey(catName))
                {
                    var cat = await context.Category.FirstOrDefaultAsync(c => c.Name == catName);
                    if (cat == null)
                    {
                        cat = new Category { Name = catName };
                        context.Category.Add(cat);
                        await context.SaveChangesAsync();
                    }
                    categories[catName] = cat;
                }

                if (!brands.ContainsKey(brName))
                {
                    var br = await context.Brand.FirstOrDefaultAsync(b => b.Name == brName);
                    if (br == null)
                    {
                        br = new Brand { Name = brName };
                        context.Brand.Add(br);
                        await context.SaveChangesAsync();
                    }
                    brands[brName] = br;
                }

                p.CategoryId = categories[catName].Id;
                p.BrandId = brands[brName].Id;
                
                // Clear navigation properties to prevent EF from trying to insert them as new entities
                p.Category = null;
                p.Brand = null;
            }

            context.Product.AddRange(baseProducts);
            await context.SaveChangesAsync();

            foreach (var p in baseProducts)
            {
                await activityLogger.LogAsync(ActivityAction.CreateProduct, $"Created product <a href='/products/details/{p.Id}' class='product-link'>{p.Name}</a>.", superAdmin?.Id, superAdmin?.UserName, "SuperAdmin", superAdmin?.FullName);
            }
        }
    }
}
