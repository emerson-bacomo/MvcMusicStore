using Microsoft.AspNetCore.Identity;
using MvcMusic.Models;
using Microsoft.Extensions.Logging;
using MvcMusic.Utils;

namespace MvcMusic.Data
{
    public static partial class DataSeeder
    {
        private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager, ILogger logger, IActivityLogService activityLogger)
        {
            logger.LogInformation("Seeding roles...");
            string[] roleNames = { "SuperAdmin", "Admin", "Staff", "User" };
            foreach (var roleName in roleNames)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    logger.LogInformation("Creating role: {RoleName}", roleName);
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                    await activityLogger.LogAsync(ActivityAction.CreateRole, $"Role '{roleName}' was created by System.", "System", "System", "System", "System");
                }
            }
        }
    }
}
