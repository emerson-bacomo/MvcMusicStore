using Microsoft.AspNetCore.Identity;
using MvcMusic.Models;
using Microsoft.Extensions.Logging;
using MvcMusic.Utils;

namespace MvcMusic.Data
{
    public static partial class DataSeeder
    {
        private static async Task<ApplicationUser?> SeedSuperAdminAsync(UserManager<ApplicationUser> userManager, ILogger logger, IActivityLogService activityLogger)
        {
            logger.LogInformation("Seeding SuperAdmin...");
            var year = DateTime.UtcNow.ToString("yy");
            
            var superAdminEmail = "superadmin@nightcord.com";
            var superAdmin = await userManager.FindByEmailAsync(superAdminEmail);
            if (superAdmin == null)
            {
                superAdmin = new ApplicationUser
                {
                    UserName = $"{year}-X0001",
                    Email = superAdminEmail,
                    FirstName = "Super",
                    LastName = "Admin",
                    EmailConfirmed = true,
                    DateCreated = DateTime.UtcNow
                };
                if ((await userManager.CreateAsync(superAdmin, "Password123")).Succeeded)
                {
                    logger.LogInformation("Created SuperAdmin user: {Email}", superAdminEmail);
                    await userManager.AddToRoleAsync(superAdmin, "SuperAdmin");
                    await activityLogger.LogAsync(ActivityAction.CreateEmployee, $"SuperAdmin account <a href='/employee/edit/{superAdmin.Id}' class='emp-link'>{superAdmin.UserName}</a> was created by System.", "System", "System", "System", "System");
                }
            }
            return await userManager.FindByEmailAsync(superAdminEmail);
        }

        private static async Task SeedStaffAsync(UserManager<ApplicationUser> userManager, ILogger logger, IActivityLogService activityLogger, ApplicationUser? superAdmin)
        {
            logger.LogInformation("Seeding staff employees...");
            var year = DateTime.UtcNow.ToString("yy");

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
                    DateCreated = DateTime.UtcNow,
                    RequiresPasswordChange = true
                };
                if ((await userManager.CreateAsync(user, "Password123")).Succeeded)
                {
                    logger.LogInformation("Created Admin user: {Email}", adminEmail);
                    await userManager.AddToRoleAsync(user, "Admin");
                    await activityLogger.LogAsync(ActivityAction.CreateEmployee, $"Admin account <a href='/employee/edit/{user.Id}' class='emp-link'>{user.UserName}</a> was created.", superAdmin?.Id, superAdmin?.UserName, "SuperAdmin", superAdmin?.FullName);
                }
            }

            // Seed StockStaff
            var stockEmail = "stockstaff@nightcord.com";
            if (await userManager.FindByEmailAsync(stockEmail) == null)
            {
                var user = new ApplicationUser { UserName = $"{year}-T0001", Email = stockEmail, FirstName = "Tina", LastName = "Stone", EmailConfirmed = true, DateCreated = DateTime.UtcNow, RequiresPasswordChange = true };
                if ((await userManager.CreateAsync(user, "Password123")).Succeeded)
                {
                    await userManager.AddToRoleAsync(user, "StockStaff");
                    await activityLogger.LogAsync(ActivityAction.CreateEmployee, $"StockStaff account {user.UserName} was created.", superAdmin?.Id, superAdmin?.UserName, "SuperAdmin", superAdmin?.FullName);
                }
            }

            // Seed ProductStaff
            var productEmail = "productstaff@nightcord.com";
            if (await userManager.FindByEmailAsync(productEmail) == null)
            {
                var user = new ApplicationUser { UserName = $"{year}-P0001", Email = productEmail, FirstName = "Paula", LastName = "Cruz", EmailConfirmed = true, DateCreated = DateTime.UtcNow, RequiresPasswordChange = true };
                if ((await userManager.CreateAsync(user, "Password123")).Succeeded)
                {
                    await userManager.AddToRoleAsync(user, "ProductStaff");
                    await activityLogger.LogAsync(ActivityAction.CreateEmployee, $"ProductStaff account {user.UserName} was created.", superAdmin?.Id, superAdmin?.UserName, "SuperAdmin", superAdmin?.FullName);
                }
            }

            // Seed SalesStaff
            var salesEmail = "salesstaff@nightcord.com";
            if (await userManager.FindByEmailAsync(salesEmail) == null)
            {
                var user = new ApplicationUser { UserName = $"{year}-L0001", Email = salesEmail, FirstName = "Leon", LastName = "Drake", EmailConfirmed = true, DateCreated = DateTime.UtcNow, RequiresPasswordChange = true };
                if ((await userManager.CreateAsync(user, "Password123")).Succeeded)
                {
                    await userManager.AddToRoleAsync(user, "SalesStaff");
                    await activityLogger.LogAsync(ActivityAction.CreateEmployee, $"SalesStaff account {user.UserName} was created.", superAdmin?.Id, superAdmin?.UserName, "SuperAdmin", superAdmin?.FullName);
                }
            }

            // Seed CustomerStaff
            var customerEmail = "customerstaff@nightcord.com";
            if (await userManager.FindByEmailAsync(customerEmail) == null)
            {
                var user = new ApplicationUser { UserName = $"{year}-C0001", Email = customerEmail, FirstName = "Clara", LastName = "Vance", EmailConfirmed = true, DateCreated = DateTime.UtcNow, RequiresPasswordChange = true };
                if ((await userManager.CreateAsync(user, "Password123")).Succeeded)
                {
                    await userManager.AddToRoleAsync(user, "CustomerStaff");
                    await activityLogger.LogAsync(ActivityAction.CreateEmployee, $"CustomerStaff account {user.UserName} was created.", superAdmin?.Id, superAdmin?.UserName, "SuperAdmin", superAdmin?.FullName);
                }
            }
        }
    }
}
