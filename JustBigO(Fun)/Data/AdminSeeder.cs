using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;

namespace JustBigO_Fun_.Data;

public static class AdminSeeder
{
    public const string AdminRole = "Admin";
    public const string AdminEmail = "admin@justbigofun.local";

    /// <summary>
    /// Configuration key: set via User Secrets or environment variables, not committed to git.
    /// Example: dotnet user-secrets set "Seeding:AdminPassword" "YourStrongPassword!" --project "JustBigO(Fun)"
    /// </summary>
    public const string AdminPasswordConfigurationKey = "Seeding:AdminPassword";

    public static async Task SeedAsync(
        RoleManager<IdentityRole> roleManager,
        UserManager<IdentityUser> userManager,
        IConfiguration configuration)
    {
        if (!await roleManager.RoleExistsAsync(AdminRole))
            await roleManager.CreateAsync(new IdentityRole(AdminRole));

        var adminPassword = configuration[AdminPasswordConfigurationKey]?.Trim();
        if (string.IsNullOrEmpty(adminPassword))
            return;

        var admin = await userManager.FindByEmailAsync(AdminEmail);
        if (admin == null)
        {
            admin = new IdentityUser
            {
                UserName = AdminEmail,
                Email = AdminEmail,
                EmailConfirmed = true
            };
            var result = await userManager.CreateAsync(admin, adminPassword);
            if (result.Succeeded)
                await userManager.AddToRoleAsync(admin, AdminRole);
        }
    }
}
