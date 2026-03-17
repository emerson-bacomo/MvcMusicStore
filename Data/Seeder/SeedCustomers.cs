using Microsoft.AspNetCore.Identity;
using MvcMusic.Models;
using Microsoft.Extensions.Logging;
using MvcMusic.Utils;

namespace MvcMusic.Data
{
    public static partial class DataSeeder
    {
        private static async Task<List<ApplicationUser>> SeedCustomersAsync(UserManager<ApplicationUser> userManager, ILogger logger, IActivityLogService activityLogger)
        {
            logger.LogInformation("Seeding customers...");
            var rng = new Random(42);
            var customerNames = new[] { "Maria Santos", "John Cruz", "Ana Reyes", "Miguel Torres", "Sofia Lim", "James Park", "Elena Gomez", "David Kim" };
            var customers = new List<ApplicationUser>();

            for (int i = 0; i < customerNames.Length; i++)
            {
                var nameParts = customerNames[i].Split(' ');
                var email = $"customer{i + 1}@gmail.com";
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
                    {
                        logger.LogInformation("Created Customer user: {Email}", email);
                        await userManager.AddToRoleAsync(user, "User");
                        await activityLogger.LogAsync(ActivityAction.Register, $"Registered a new <a href='/customers/profile/{user.Id}' class='customer-link'>account</a>.", user.Id, user.UserName, "User", user.FullName);
                    }
                }
                customers.Add(user);
            }
            return customers;
        }
    }
}
