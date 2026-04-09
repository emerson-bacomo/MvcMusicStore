using Microsoft.AspNetCore.Identity;
using MvcMusic.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MvcMusic.Utils;

namespace MvcMusic.Data
{
    public static partial class DataSeeder
    {
        public class SeedOptions
        {
            public bool SeedProducts { get; set; } = true;
            public bool SeedEmployees { get; set; } = true;
            public bool SeedCustomers { get; set; } = true;
            public bool SeedOrders { get; set; } = true;
        }
        public static async Task SeedAllAsync(IServiceProvider serviceProvider, SeedOptions? options = null)
        {
            options ??= new SeedOptions();
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<MvcMusicContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DataSeeder");
            var activityLogger = scope.ServiceProvider.GetRequiredService<IActivityLogService>();

            logger.LogInformation("Starting data seeding process...");

            await SeedRolesAsync(roleManager, logger, activityLogger);
            
            // SuperAdmin is ALWAYS seeded
            var superAdmin = await SeedSuperAdminAsync(userManager, logger, activityLogger);

            if (options.SeedEmployees)
            {
                await SeedStaffAsync(userManager, logger, activityLogger, superAdmin);
            }

            if (options.SeedCustomers)
            {
                var customers = await SeedCustomersAsync(userManager, logger, activityLogger);
                
                if (options.SeedProducts)
                {
                    await SeedProductsAsync(context, logger, activityLogger, superAdmin);
                    
                    if (options.SeedOrders)
                    {
                        await SeedOrdersAsync(context, customers, logger, activityLogger);
                    }
                }
            }
            else if (options.SeedProducts)
            {
                await SeedProductsAsync(context, logger, activityLogger, superAdmin);
            }

            logger.LogInformation("Data seeding process completed successfully.");
        }
    }
}
