using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using JustBigO_Fun_.Data;
using JustBigO_Fun_.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JustBigO_Fun_.Controllers.Admin;

[Authorize(Roles = AdminSeeder.AdminRole)]
[Area("Admin")]
[Route("Admin/[controller]")]
public class ProblemsController : Controller
{
    private readonly ApplicationDbContext _db;

    public ProblemsController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var problems = await _db.Problems
            .OrderBy(p => p.OrderIndex)
            .Select(p => new ProblemListVm(p.Id, p.Title, p.Slug, p.Difficulty, p.OrderIndex, p.Tests.Count))
            .ToListAsync();
        return View(problems);
    }

    [HttpGet("Create")]
    public IActionResult Create() => View(ProblemEditVm.WithDefaultTemplates());

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProblemEditVm vm, CancellationToken ct)
    {
        if (await _db.Problems.AnyAsync(p => p.Slug == vm.Slug, ct))
            ModelState.AddModelError(nameof(vm.Slug), "A problem with this slug already exists.");

        if (ModelState.IsValid)
        {
            var problem = new Problem
            {
                Title = vm.Title,
                Slug = vm.Slug,
                // Stored as raw Markdown; rendered to sanitized HTML at display time.
                Description = vm.Description ?? "",
                Difficulty = vm.Difficulty ?? "Easy",
                Tags = vm.Tags ?? "",
                CodeTemplatesJson = vm.BuildCodeTemplatesJson(),
                // OrderIndex is managed by the backend, not entered by the admin.
                OrderIndex = await NextOrderIndexAsync(ct)
            };
            _db.Problems.Add(problem);
            await _db.SaveChangesAsync(ct);

            if (!string.IsNullOrWhiteSpace(vm.FirstTestInput) && !string.IsNullOrWhiteSpace(vm.FirstTestExpectedOutput))
            {
                _db.ProblemTests.Add(new ProblemTest
                {
                    ProblemId = problem.Id,
                    InputJson = vm.FirstTestInput,
                    ExpectedOutputJson = vm.FirstTestExpectedOutput.TrimEnd(),
                    OrderIndex = 1
                });
                await _db.SaveChangesAsync(ct);
            }

            if (vm.InFiles?.Any() == true && vm.OutFiles?.Any() == true)
                await SaveTestFilesAsync(problem.Id, vm.InFiles, vm.OutFiles, ct);

            await _db.SaveChangesAsync(ct);
            return RedirectToAction(nameof(Edit), new { id = problem.Id });
        }
        return View(vm);
    }

    [HttpGet("Edit/{id:int}")]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var p = await _db.Problems
            .Include(x => x.Tests.OrderBy(t => t.OrderIndex))
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p == null) return NotFound();

        var vm = BuildEditVm(p);
        return View(vm);
    }

    [HttpPost("Edit/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ProblemEditVm vm, CancellationToken ct)
    {
        if (id != vm.Id) return BadRequest();
        var existing = await _db.Problems
            .Include(x => x.Tests.OrderBy(t => t.OrderIndex))
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (existing == null) return NotFound();

        if (await _db.Problems.AnyAsync(p => p.Slug == vm.Slug && p.Id != id, ct))
            ModelState.AddModelError(nameof(vm.Slug), "A problem with this slug already exists.");

        if (ModelState.IsValid)
        {
            existing.Title = vm.Title;
            existing.Slug = vm.Slug;
            // Stored as raw Markdown; rendered to sanitized HTML at display time.
            existing.Description = vm.Description ?? "";
            existing.Difficulty = vm.Difficulty ?? "Easy";
            existing.Tags = vm.Tags ?? "";
            existing.CodeTemplatesJson = vm.BuildCodeTemplatesJson();
            // OrderIndex is intentionally not updated here; it is backend-managed.

            if (vm.InFiles?.Any() == true && vm.OutFiles?.Any() == true)
                await SaveTestFilesAsync(existing.Id, vm.InFiles, vm.OutFiles, ct);

            await _db.SaveChangesAsync(ct);
            TempData["AdminMessage"] = "Problem saved.";
            return RedirectToAction(nameof(Edit), new { id = existing.Id });
        }

        // Re-populate the read-only/list portions of the VM before redisplaying the form.
        vm.OrderIndex = existing.OrderIndex;
        vm.Tests = existing.Tests.Select(ToTestVm).ToList();
        return View(vm);
    }

    [HttpPost("Delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var p = await _db.Problems.FindAsync([id], ct);
        if (p == null) return NotFound();
        _db.Problems.Remove(p);
        await _db.SaveChangesAsync(ct);
        return RedirectToAction(nameof(Index));
    }

    // ---------- Test case CRUD ----------

    [HttpPost("{problemId:int}/Tests/Add")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddTest(int problemId, string inputJson, string expectedOutputJson, CancellationToken ct)
    {
        var problem = await _db.Problems.FindAsync([problemId], ct);
        if (problem == null) return NotFound();

        if (string.IsNullOrWhiteSpace(inputJson) || string.IsNullOrWhiteSpace(expectedOutputJson))
        {
            TempData["AdminError"] = "Both input and expected output are required to add a test.";
            return RedirectToAction(nameof(Edit), new { id = problemId });
        }

        var maxOrder = await _db.ProblemTests
            .Where(t => t.ProblemId == problemId)
            .Select(t => (int?)t.OrderIndex)
            .MaxAsync(ct) ?? 0;

        _db.ProblemTests.Add(new ProblemTest
        {
            ProblemId = problemId,
            InputJson = inputJson,
            ExpectedOutputJson = expectedOutputJson.TrimEnd(),
            OrderIndex = maxOrder + 1
        });
        await _db.SaveChangesAsync(ct);
        TempData["AdminMessage"] = "Test added.";
        return RedirectToAction(nameof(Edit), new { id = problemId });
    }

    [HttpPost("{problemId:int}/Tests/{testId:int}/Edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditTest(int problemId, int testId, string inputJson, string expectedOutputJson, CancellationToken ct)
    {
        var test = await _db.ProblemTests.FirstOrDefaultAsync(t => t.Id == testId && t.ProblemId == problemId, ct);
        if (test == null) return NotFound();

        if (string.IsNullOrWhiteSpace(inputJson) || string.IsNullOrWhiteSpace(expectedOutputJson))
        {
            TempData["AdminError"] = "Both input and expected output are required.";
            return RedirectToAction(nameof(Edit), new { id = problemId });
        }

        test.InputJson = inputJson;
        test.ExpectedOutputJson = expectedOutputJson.TrimEnd();
        await _db.SaveChangesAsync(ct);
        TempData["AdminMessage"] = "Test updated.";
        return RedirectToAction(nameof(Edit), new { id = problemId });
    }

    [HttpPost("{problemId:int}/Tests/{testId:int}/Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteTest(int problemId, int testId, CancellationToken ct)
    {
        var test = await _db.ProblemTests.FirstOrDefaultAsync(t => t.Id == testId && t.ProblemId == problemId, ct);
        if (test == null) return NotFound();

        _db.ProblemTests.Remove(test);
        await _db.SaveChangesAsync(ct);
        TempData["AdminMessage"] = "Test deleted.";
        return RedirectToAction(nameof(Edit), new { id = problemId });
    }

    // ---------- Helpers ----------

    private async Task<int> NextOrderIndexAsync(CancellationToken ct)
    {
        var max = await _db.Problems.Select(p => (int?)p.OrderIndex).MaxAsync(ct) ?? 0;
        return max + 1;
    }

    private ProblemEditVm BuildEditVm(Problem p)
    {
        var templates = p.GetCodeTemplates();
        return new ProblemEditVm
        {
            Id = p.Id,
            Title = p.Title,
            Slug = p.Slug,
            Description = p.Description,
            Difficulty = p.Difficulty,
            Tags = p.Tags,
            OrderIndex = p.OrderIndex,
            PythonTemplate = templates.GetValueOrDefault("python", ""),
            JavaTemplate = templates.GetValueOrDefault("java", ""),
            CppTemplate = templates.GetValueOrDefault("cpp", ""),
            Tests = p.Tests.OrderBy(t => t.OrderIndex).Select(ToTestVm).ToList()
        };
    }

    private static ProblemTestVm ToTestVm(ProblemTest t) =>
        new() { Id = t.Id, OrderIndex = t.OrderIndex, InputJson = t.InputJson, ExpectedOutputJson = t.ExpectedOutputJson };

    private async Task SaveTestFilesAsync(int problemId, List<IFormFile> inFiles, List<IFormFile> outFiles, CancellationToken ct)
    {
        var maxOrder = await _db.ProblemTests
            .Where(t => t.ProblemId == problemId)
            .Select(t => (int?)t.OrderIndex)
            .MaxAsync(ct) ?? 0;

        var inList = inFiles.OrderBy(f => f.FileName).ToList();
        var outList = outFiles.OrderBy(f => f.FileName).ToList();
        var pairCount = Math.Min(inList.Count, outList.Count);

        for (var i = 0; i < pairCount; i++)
        {
            using var inStream = inList[i].OpenReadStream();
            using var outStream = outList[i].OpenReadStream();
            using var inReader = new StreamReader(inStream);
            using var outReader = new StreamReader(outStream);
            var inputContent = await inReader.ReadToEndAsync(ct);
            var outputContent = await outReader.ReadToEndAsync(ct);

            _db.ProblemTests.Add(new ProblemTest
            {
                ProblemId = problemId,
                InputJson = inputContent,
                ExpectedOutputJson = outputContent.TrimEnd(),
                OrderIndex = ++maxOrder
            });
        }
    }
}

public record ProblemListVm(int Id, string Title, string Slug, string Difficulty, int OrderIndex, int TestCount);

public class ProblemTestVm
{
    public int Id { get; set; }
    public int OrderIndex { get; set; }
    public string InputJson { get; set; } = "";
    public string ExpectedOutputJson { get; set; } = "";
}

public class ProblemEditVm
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = "";

    [Required]
    [MaxLength(200)]
    public string Slug { get; set; } = "";

    [Required]
    public string Description { get; set; } = "";

    [MaxLength(20)]
    public string Difficulty { get; set; } = "Easy";

    [MaxLength(500)]
    public string Tags { get; set; } = "";

    // Per-language code templates. The backend bundles these into CodeTemplatesJson on save,
    // so admins never have to hand-write JSON.
    [Display(Name = "Python template")]
    public string? PythonTemplate { get; set; }

    [Display(Name = "Java template")]
    public string? JavaTemplate { get; set; }

    [Display(Name = "C++ template")]
    public string? CppTemplate { get; set; }

    /// <summary>Read-only; assigned automatically by the backend.</summary>
    public int OrderIndex { get; set; }

    public List<IFormFile>? InFiles { get; set; }
    public List<IFormFile>? OutFiles { get; set; }

    // Optional single test case captured when creating a new problem.
    public string? FirstTestInput { get; set; }
    public string? FirstTestExpectedOutput { get; set; }

    /// <summary>Existing test cases for the problem (Edit view only).</summary>
    public List<ProblemTestVm> Tests { get; set; } = new();

    /// <summary>Bundles the per-language templates into the JSON shape the rest of the app expects.</summary>
    public string BuildCodeTemplatesJson()
    {
        var map = new Dictionary<string, string>();
        if (!string.IsNullOrWhiteSpace(PythonTemplate)) map["python"] = PythonTemplate;
        if (!string.IsNullOrWhiteSpace(JavaTemplate)) map["java"] = JavaTemplate;
        if (!string.IsNullOrWhiteSpace(CppTemplate)) map["cpp"] = CppTemplate;
        return JsonSerializer.Serialize(map);
    }

    /// <summary>Boilerplate starter code pre-filled when creating a new problem.</summary>
    public const string DefaultPythonTemplate =
        "import sys\n\ndef main():\n    # read from stdin, write the answer to stdout\n    pass\n\nif __name__ == \"__main__\":\n    main()\n";

    public const string DefaultJavaTemplate =
        "import java.util.*;\n\npublic class Main {\n    public static void main(String[] args) {\n        Scanner sc = new Scanner(System.in);\n        // read from stdin, print the answer to stdout\n    }\n}\n";

    public const string DefaultCppTemplate =
        "#include <iostream>\n#include <vector>\n\nusing namespace std;\n\nint main() {\n    // read from stdin, print the answer to stdout\n    return 0;\n}\n";

    /// <summary>Creates a VM pre-populated with default per-language code templates.</summary>
    public static ProblemEditVm WithDefaultTemplates() => new()
    {
        PythonTemplate = DefaultPythonTemplate,
        JavaTemplate = DefaultJavaTemplate,
        CppTemplate = DefaultCppTemplate
    };
}
