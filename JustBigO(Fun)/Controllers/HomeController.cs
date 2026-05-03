using System.Diagnostics;
using JustBigO_Fun_.Data;
using JustBigO_Fun_.Models;
using JustBigO_Fun_.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
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

        // --- INCEPUT MODIFICARE ---
        // Am adăugat parametrul `difficultyFilter` și am implementat logica de filtrare `.Where(...)`
        public async Task<IActionResult> Index(string sortOrder, string difficultyFilter)
        {
            // Salvăm parametrii în ViewData pentru a menține starea în butoanele de pe UI
            ViewData["DifficultySortParm"] = sortOrder == "diff_asc" ? "diff_desc" : "diff_asc";
            ViewData["CurrentFilter"] = difficultyFilter;

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var problems = await _db.Problems.ToListAsync();

            var userBestStatuses = new Dictionary<int, string>();
            if (userId != null)
            {
                var submissions = await _db.Submissions
                    .Where(s => s.UserId == userId)
                    .Select(s => new { s.ProblemId, s.Status })
                    .ToListAsync();

                foreach (var sub in submissions)
                {
                    string statusStr = sub.Status.ToString();
                    if (!userBestStatuses.ContainsKey(sub.ProblemId) || statusStr == "Accepted")
                    {
                        userBestStatuses[sub.ProblemId] = statusStr;
                    }
                }
            }

            var problemItems = problems.Select(p => new ProblemListItem(
                p.Id,
                p.Title,
                p.Tags,
                p.Difficulty,
                userBestStatuses.GetValueOrDefault(p.Id, "—")
            )).AsEnumerable();

            // Dacă s-a selectat o dificultate, aplicăm filtrul
            if (!string.IsNullOrEmpty(difficultyFilter))
            {
                problemItems = problemItems.Where(p => string.Equals(p.Difficulty, difficultyFilter, StringComparison.OrdinalIgnoreCase));
            }

            // Aplicăm sortarea pe lista (posibil deja filtrată)
            problemItems = sortOrder switch
            {
                "diff_asc" => problemItems.OrderBy(p => GetDifficultyWeight(p.Difficulty)),
                "diff_desc" => problemItems.OrderByDescending(p => GetDifficultyWeight(p.Difficulty)),
                _ => problemItems.OrderBy(p => problems.First(x => x.Id == p.Id).OrderIndex)
            };

            return View(problemItems.ToList());
        }

        private static int GetDifficultyWeight(string difficulty)
        {
            return difficulty?.ToLowerInvariant() switch
            {
                "easy" => 1,
                "medium" => 2,
                "hard" => 3,
                _ => 0
            };
        }
        // --- SFARSIT MODIFICARE ---

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

        [HttpPost]
        public async Task<IActionResult> AnalyzeComplexity(
    [FromBody] SubmissionViewModel model,
    [FromServices] IComplexityAnalyzer complexityAnalyzer)
        {
            if (string.IsNullOrWhiteSpace(model.SourceCode)) return BadRequest("Codul este gol.");

            // DECOMENTĂM ASTA CA SĂ MEARGĂ PE BUNE:
            var complexity = await complexityAnalyzer.AnalyzeCodeAsync(model.SourceCode);

            // RETURNĂM REZULTATUL REAL:
            return Json(new
            {
                timeComplexity = complexity.TimeComplexity,
                spaceComplexity = complexity.SpaceComplexity
            });
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> GetHint(
            [FromBody] SubmissionViewModel model,
            [FromServices] IHintGenerator hintGenerator)
        {
            if (model.ProblemId <= 0)
                return BadRequest("ProblemId invalid.");

            var problem = await _db.Problems.FirstOrDefaultAsync(p => p.Id == model.ProblemId);
            if (problem == null)
                return NotFound("Problema nu a fost gasita.");

            var hint = await hintGenerator.GenerateHintAsync(
                problem.Title,
                problem.Description,
                model.SourceCode ?? string.Empty,
                string.IsNullOrWhiteSpace(model.Language) ? "unknown" : model.Language);

            return Json(new { hint });
        }

        [HttpPost]
        public async Task<IActionResult> SubmitSolution(
            [FromBody] SubmissionViewModel model,
            [FromServices] ICodeExecutor codeExecutor,
            [FromServices] IComplexityAnalyzer complexityAnalyzer)
        {
            if (!ModelState.IsValid) return BadRequest("Date invalide.");

            // 1. Executăm codul prin Docker
            await codeExecutor.ExecuteAsync(model.ProblemId);

            // TODO: Aici îți pui logica ta prin care citești dacă testele au trecut
            // Momentan simulăm succesul pentru a vedea AI-ul în acțiune pe interfață
            bool isSuccess = true;
            string testCasesJson = "[]";

            string timeO = "O(?)";
            string spaceO = "O(?)";

            // 2. Dacă codul trece testele, apelăm Agentul AI (Acceptance Criteria)
            if (isSuccess)
            {
                var complexity = await complexityAnalyzer.AnalyzeCodeAsync(model.SourceCode);
                timeO = complexity.TimeComplexity;
                spaceO = complexity.SpaceComplexity;
            }

            // 3. Returnăm formatul exact pe care îl așteaptă JavaScript-ul
            return Json(new
            {
                status = isSuccess ? "Accepted" : "Failed",
                results = testCasesJson,
                timeComplexity = timeO,
                spaceComplexity = spaceO
            });
        }
    }
}