using JustBigO_Fun_.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JustBigO_Fun_.Controllers.Admin;

[Authorize(Roles = AdminSeeder.AdminRole)]
[Area("Admin")]
[Route("Admin/[controller]")]
public class UsersController : Controller
{
    /// <summary>
    /// Pseudo-role shown in the UI for a user that has no Identity role assigned.
    /// This app uses a single-role-per-user model, so "no role" maps to a plain "User".
    /// </summary>
    public const string NoRole = "User";

    private readonly UserManager<IdentityUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public UsersController(UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager)
    {
        _userManager = userManager;
        _roleManager = roleManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var currentUserId = _userManager.GetUserId(User);

        // "User" (no role) first, then every role defined in the system.
        var available = new List<string> { NoRole };
        available.AddRange(await _roleManager.Roles
            .Where(r => r.Name != null)
            .Select(r => r.Name!)
            .OrderBy(name => name)
            .ToListAsync());

        var users = await _userManager.Users
            .OrderBy(u => u.Email)
            .ToListAsync();

        var items = new List<UserListItemVm>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            items.Add(new UserListItemVm(
                user.Id,
                user.Email ?? "",
                user.UserName ?? "",
                roles.FirstOrDefault() ?? NoRole,
                user.Id == currentUserId));
        }

        return View(new UsersIndexVm { Users = items, AvailableRoles = available });
    }

    [HttpPost("UpdateRole")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateRole(string userId, string role)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return NotFound();

        // Admins cannot change their own role: avoids accidentally locking yourself out.
        if (userId == _userManager.GetUserId(User))
        {
            TempData["AdminError"] = "You cannot change your own role.";
            return RedirectToAction(nameof(Index));
        }

        if (role != NoRole && !await _roleManager.RoleExistsAsync(role))
        {
            TempData["AdminError"] = $"Role '{role}' does not exist.";
            return RedirectToAction(nameof(Index));
        }

        var currentRoles = await _userManager.GetRolesAsync(user);

        // Never allow demoting the last remaining administrator.
        if (currentRoles.Contains(AdminSeeder.AdminRole) && role != AdminSeeder.AdminRole)
        {
            var adminCount = (await _userManager.GetUsersInRoleAsync(AdminSeeder.AdminRole)).Count;
            if (adminCount <= 1)
            {
                TempData["AdminError"] = "Cannot remove the last administrator.";
                return RedirectToAction(nameof(Index));
            }
        }

        // Single-role model: clear any existing roles, then assign the chosen one
        // ("User" means no Identity role at all).
        if (currentRoles.Count > 0)
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
        if (role != NoRole)
            await _userManager.AddToRoleAsync(user, role);

        TempData["AdminMessage"] = $"Updated {user.Email} to role \"{role}\".";
        return RedirectToAction(nameof(Index));
    }
}

public record UserListItemVm(string Id, string Email, string UserName, string Role, bool IsCurrentUser);

public class UsersIndexVm
{
    public List<UserListItemVm> Users { get; set; } = new();
    public List<string> AvailableRoles { get; set; } = new();
}
