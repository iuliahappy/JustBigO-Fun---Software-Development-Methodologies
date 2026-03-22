using System.Diagnostics;
using JustBigO_Fun_.Data;
using JustBigO_Fun_.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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