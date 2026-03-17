using Microsoft.AspNetCore.Identity;
using MvcMusic.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MvcMusic.Utils;

namespace MvcMusic.Data
{
    public static partial class DataSeeder
    {
        public static async Task SeedAllAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<MvcMusicContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DataSeeder");
            var activityLogger = scope.ServiceProvider.GetRequiredService<IActivityLogService>();

            logger.LogInformation("Starting data seeding process...");

            await SeedRolesAsync(roleManager, logger, activityLogger);
            var superAdmin = await SeedEmployeesAsync(userManager, logger, activityLogger);
            var customers = await SeedCustomersAsync(userManager, logger, activityLogger);
            await SeedProductsAsync(context, logger, activityLogger, superAdmin);
            await SeedOrdersAsync(context, customers, logger, activityLogger);

            logger.LogInformation("Data seeding process completed successfully.");
        }
    }
}
