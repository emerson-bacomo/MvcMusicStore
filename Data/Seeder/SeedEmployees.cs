using Microsoft.AspNetCore.Identity;
using MvcMusic.Models;
using Microsoft.Extensions.Logging;
using MvcMusic.Utils;

namespace MvcMusic.Data
{
    public static partial class DataSeeder
    {
        private static async Task<ApplicationUser?> SeedEmployeesAsync(UserManager<ApplicationUser> userManager, ILogger logger, IActivityLogService activityLogger)
        {
            logger.LogInformation("Seeding employees...");
            var year = DateTime.UtcNow.ToString("yy");
            ApplicationUser? superAdmin = null;

            // Seed SuperAdmin
            var superAdminEmail = "superadmin@nightcord.com";
            superAdmin = await userManager.FindByEmailAsync(superAdminEmail);
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
                if ((await userManager.CreateAsync(superAdmin, "SuperAdmin@123")).Succeeded)
                {
                    logger.LogInformation("Created SuperAdmin user: {Email}", superAdminEmail);
                    await userManager.AddToRoleAsync(superAdmin, "SuperAdmin");
                    await activityLogger.LogAsync(ActivityAction.CreateEmployee, $"SuperAdmin account <a href='/employee/edit/{superAdmin.Id}' class='emp-link'>{superAdmin.UserName}</a> was created by System.", "System", "System", "System", "System");
                }
            }
            
            // Re-fetch SuperAdmin to ensure we have the ID and roles for attribution
            superAdmin = await userManager.FindByEmailAsync(superAdminEmail);

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
                {
                    logger.LogInformation("Created Admin user: {Email}", adminEmail);
                    await userManager.AddToRoleAsync(user, "Admin");
                    await activityLogger.LogAsync(ActivityAction.CreateEmployee, $"Admin account <a href='/employee/edit/{user.Id}' class='emp-link'>{user.UserName}</a> was created.", superAdmin?.Id, superAdmin?.UserName, "SuperAdmin", superAdmin?.FullName);
                }
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
                {
                    logger.LogInformation("Created Staff user: {Email}", staffEmail);
                    await userManager.AddToRoleAsync(user, "Staff");
                    await activityLogger.LogAsync(ActivityAction.CreateEmployee, $"Staff account <a href='/employee/edit/{user.Id}' class='emp-link'>{user.UserName}</a> was created.", superAdmin?.Id, superAdmin?.UserName, "SuperAdmin", superAdmin?.FullName);
                }
            }

            return superAdmin;
        }
    }
}
