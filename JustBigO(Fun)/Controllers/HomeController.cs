// MDSP/JustBigO-Fun---Software-Development-Methodologies/JustBigO(Fun)/Controllers/HomeController.cs

using System.Diagnostics;
using JustBigO_Fun_.Data;
using JustBigO_Fun_.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace JustBigO_Fun_.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _db;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext db)
        {
            _logger = logger;
            _db = db;
        }

        public async Task<IActionResult> Index()
        {
            var problems = await _db.Problems
                .OrderBy(p => p.OrderIndex)
                .Select(p => new ProblemListItem(p.Id, p.Title, p.Tags, p.Difficulty))
                .ToListAsync();
            return View(problems);
        }

        public async Task<IActionResult> Solve(int? id)
        {
            Problem? problem;
            if (id.HasValue)
            {
                problem = await _db.Problems
                    .Include(p => p.Tests)
                    .FirstOrDefaultAsync(p => p.Id == id.Value);
                if (problem == null)
                    return NotFound();
            }
            else
            {
                problem = await _db.Problems
                    .Include(p => p.Tests)
                    .OrderBy(p => p.OrderIndex)
                    .FirstOrDefaultAsync();
                if (problem == null)
                    return RedirectToAction(nameof(Index));
            }
            return View(problem);
        }

        [Authorize]
        public async Task<IActionResult> MySubmissions(int? problemId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var query = _db.Submissions
                .Where(s => s.UserId == userId)
                .Include(s => s.Problem)
                .AsQueryable();

            if (problemId.HasValue)
            {
                query = query.Where(s => s.ProblemId == problemId.Value);
                var problem = await _db.Problems.FindAsync(problemId.Value);
                // MODIFICARE: Tradus în engleză
                ViewData["Subtitle"] = $"for problem: {problem?.Title}";
            }

            var submissions = await query.OrderByDescending(s => s.CreatedAt).ToListAsync();
            return View(submissions);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}