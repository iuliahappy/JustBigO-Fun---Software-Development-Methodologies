using Microsoft.AspNetCore.Identity;

namespace JustBigO_Fun_.Data;

public static class AdminSeeder
{
    public const string AdminRole = "Admin";
    public const string AdminEmail = "admin@justbigofun.local";
    public const string DefaultAdminPassword = "Admin123!";

    public static async Task SeedAsync(RoleManager<IdentityRole> roleManager, UserManager<IdentityUser> userManager)
    {
        if (!await roleManager.RoleExistsAsync(AdminRole))
            await roleManager.CreateAsync(new IdentityRole(AdminRole));

        var admin = await userManager.FindByEmailAsync(AdminEmail);
        if (admin == null)
        {
            admin = new IdentityUser
            {
                UserName = AdminEmail,
                Email = AdminEmail,
                EmailConfirmed = true
            };
            var result = await userManager.CreateAsync(admin, DefaultAdminPassword);
            if (result.Succeeded)
                await userManager.AddToRoleAsync(admin, AdminRole);
        }
    }
}
