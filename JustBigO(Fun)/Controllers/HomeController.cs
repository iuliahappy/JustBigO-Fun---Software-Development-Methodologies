using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using JustBigO_Fun_.Data;
using JustBigO_Fun_.Models;
using JustBigO_Fun_.Services;
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
        private readonly IMarkdownRenderer _markdown;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext db, IMarkdownRenderer markdown)
        {
            _logger = logger;
            _db = db;
            _markdown = markdown;
        }

        public IActionResult Index()
        {
            return View();
        }

        public async Task<IActionResult> Problems(string sortOrder, string difficultyFilter)
        {
            var problemItems = await BuildProblemListAsync(sortOrder, difficultyFilter);
            return View(problemItems);
        }

        private async Task<List<ProblemListItem>> BuildProblemListAsync(string sortOrder, string difficultyFilter)
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

            return problemItems.ToList();
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

            // Descriptions are authored in Markdown. Render to sanitized HTML before display
            // (the view emits this via @Html.Raw). This is a read-only projection; the entity
            // is not persisted from this action. Legacy HTML-authored descriptions still render
            // correctly because Markdig passes raw HTML through, and the output is sanitized.
            problem.Description = _markdown.RenderToSafeHtml(problem.Description);

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

            try
            {
                // Gestionăm timeout-ul de 30 secunde să nu blocăm interfața.
                var analyzeTask = complexityAnalyzer.AnalyzeCodeAsync(model.SourceCode);
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30));

                if (await Task.WhenAny(analyzeTask, timeoutTask) == analyzeTask)
                {
                    var complexity = await analyzeTask;
                    return Json(new
                    {
                        timeComplexity = complexity.TimeComplexity,
                        spaceComplexity = complexity.SpaceComplexity
                    });
                }

                return StatusCode(504, "Timeout: Agentul AI a durat prea mult.");
            }
            catch (Exception)
            {
                return StatusCode(500, "Eroare internă a agentului AI.");
            }
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> GetHint(
            [FromBody] SubmissionViewModel model,
            [FromServices] IHintGenerator hintGenerator)
        {
            if (model.ProblemId <= 0)
                return BadRequest("ProblemId invalid.");

            //this is a command to stop the AI enough in order to show the 10s error
            //await Task.Delay(15000);

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
        [Authorize]
        public async Task<IActionResult> CompleteCurrentCode(
            [FromBody] SubmissionViewModel model,
            [FromServices] ICurrentCodeCompletionService completionService,
            CancellationToken cancellationToken)
        {
            if (model.ProblemId <= 0)
                return BadRequest("Invalid ProblemId.");
            if (string.IsNullOrWhiteSpace(model.SourceCode))
                return BadRequest("Source code is empty.");

            var lang = string.IsNullOrWhiteSpace(model.Language) ? "python" : model.Language;

            var result = await completionService.CompleteAsync(
                model.ProblemId,
                model.SourceCode,
                lang,
                cancellationToken);

            return Json(new
            {
                code = result.Code,
                approachSummary = result.ApproachSummary,
                testsPassed = result.TestsPassed,
                status = result.LastStatus.ToString(),
                results = result.ResultsJson,
                message = result.Message
            });
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> BestSubmissionStatus(int problemId, CancellationToken cancellationToken)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            // "Current" outcome: latest submission only (not an older Accepted if user later failed).
            var latestStatus = await _db.Submissions
                .Where(s => s.UserId == userId && s.ProblemId == problemId)
                .OrderByDescending(s => s.CreatedAt)
                .Select(s => s.Status)
                .FirstOrDefaultAsync(cancellationToken);

            var hasAccepted = latestStatus == SubmissionStatus.Accepted;

            return Json(new { hasAccepted, bestStatus = hasAccepted ? "Accepted" : "NotAccepted" });
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> ImproveSolution(
            [FromBody] SubmissionViewModel model,
            [FromServices] IRefactoringSuggestionGenerator refactoringGenerator,
            CancellationToken cancellationToken)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            if (model.ProblemId <= 0)
                return BadRequest("Invalid ProblemId.");
            if (string.IsNullOrWhiteSpace(model.SourceCode))
                return BadRequest("Source code is required.");

            var latestStatus = await _db.Submissions
                .Where(s => s.UserId == userId && s.ProblemId == model.ProblemId)
                .OrderByDescending(s => s.CreatedAt)
                .Select(s => s.Status)
                .FirstOrDefaultAsync(cancellationToken);

            if (latestStatus != SubmissionStatus.Accepted)
            {
                return BadRequest(
                    "Improve solution is only available when your latest submission for this problem is Accepted (all Docker tests passed).");
            }

            var problem = await _db.Problems.FirstOrDefaultAsync(p => p.Id == model.ProblemId, cancellationToken);
            if (problem == null)
                return NotFound("Problem not found.");

            var lang = string.IsNullOrWhiteSpace(model.Language) ? "python" : model.Language;
            var suggestion = await refactoringGenerator.GenerateAsync(
                problem.Title,
                problem.Description,
                model.SourceCode,
                lang,
                cancellationToken);

            return Json(new
            {
                codeBlockToModify = suggestion.CodeBlockToModify,
                optimalDataStructure = suggestion.OptimalDataStructure,
                refactoringSteps = suggestion.RefactoringSteps
            });
        }

        [HttpPost]
        public async Task<IActionResult> SubmitSolution(
            [FromBody] SubmissionViewModel model,
            [FromServices] ICodeExecutor codeExecutor,
            [FromServices] IComplexityAnalyzer complexityAnalyzer)
        {
            if (!ModelState.IsValid) return BadRequest("Date invalide.");

            // 1. Executăm codul prin Docker (Garantăm rularea neobstrucționată)
            await codeExecutor.ExecuteAsync(model.ProblemId);

            // TODO: Aici îți pui logica ta prin care citești dacă testele au trecut
            bool isSuccess = true;
            string testCasesJson = "[]";

            string timeO = "O(?)";
            string spaceO = "O(?)";

            // 2. Dacă codul trece testele, apelăm Agentul AI cu TIMEOUT de 30 secunde
            if (isSuccess)
            {
                try
                {
                    var analyzeTask = complexityAnalyzer.AnalyzeCodeAsync(model.SourceCode);
                    var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30));

                    if (await Task.WhenAny(analyzeTask, timeoutTask) == analyzeTask)
                    {
                        var complexity = await analyzeTask;
                        timeO = complexity.TimeComplexity;
                        spaceO = complexity.SpaceComplexity;
                    }
                    else
                    {
                        // Timeout depășit, AI-ul pică, nu blocăm Docker-ul.
                        timeO = "Timeout AI";
                        spaceO = "Timeout AI";
                    }
                }
                catch
                {
                    timeO = "Eroare AI";
                    spaceO = "Eroare AI";
                }
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