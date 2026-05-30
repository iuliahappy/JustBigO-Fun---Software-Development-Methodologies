using JustBigO_Fun_.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JustBigO_Fun_.Controllers.Admin;

[Authorize(Roles = AdminSeeder.AdminRole)]
[Area("Admin")]
[Route("Admin")]
public class DashboardController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;

    public DashboardController(ApplicationDbContext db, UserManager<IdentityUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var vm = new AdminDashboardVm
        {
            ProblemCount = await _db.Problems.CountAsync(ct),
            TestCount = await _db.ProblemTests.CountAsync(ct),
            SubmissionCount = await _db.Submissions.CountAsync(ct),
            UserCount = await _userManager.Users.CountAsync(ct),
            AdminCount = (await _userManager.GetUsersInRoleAsync(AdminSeeder.AdminRole)).Count
        };
        return View(vm);
    }
}

public class AdminDashboardVm
{
    public int ProblemCount { get; set; }
    public int TestCount { get; set; }
    public int SubmissionCount { get; set; }
    public int UserCount { get; set; }
    public int AdminCount { get; set; }
}
