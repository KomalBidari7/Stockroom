using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Stockroom.Domain.Common;

namespace Stockroom.Infrastructure.Identity;

public static class IdentitySeeder
{
    public static async Task SeedRolesAndAdminAsync(
        RoleManager<IdentityRole> roleManager,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        ILogger logger)
    {
        // 1. Ensure all defined roles exist (idempotent)
        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                logger.LogInformation("Seeding role: {Role}", role);
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        // 2. Safe initial administrator provisioning
        var adminEmail = configuration["InitialAdmin:Email"] ?? "admin@stockroom.local";
        var adminPassword = configuration["InitialAdmin:Password"] ?? "AdminPassword123!";

        var existingAdmin = await userManager.FindByEmailAsync(adminEmail);
        if (existingAdmin == null)
        {
            logger.LogInformation("Provisioning initial administrator account for {AdminEmail}", adminEmail);

            var adminUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                CreatedAtUtc = DateTime.UtcNow
            };

            var createResult = await userManager.CreateAsync(adminUser, adminPassword);
            if (createResult.Succeeded)
            {
                await userManager.AddToRolesAsync(adminUser, [Roles.Admin, Roles.InventoryManager, Roles.Viewer]);
                logger.LogInformation("Initial administrator successfully created and assigned Admin roles.");
            }
            else
            {
                logger.LogWarning("Failed to provision initial administrator: {Errors}",
                    string.Join("; ", createResult.Errors.Select(e => e.Description)));
            }
        }
        else
        {
            // Do NOT reset passwords or alter existing privileges for existing users
            logger.LogDebug("Initial administrator account {AdminEmail} already exists. Skipping provisioning.", adminEmail);
        }
    }
}
