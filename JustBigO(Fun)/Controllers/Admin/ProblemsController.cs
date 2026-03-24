using System.ComponentModel.DataAnnotations;
using JustBigO_Fun_.Data;
using JustBigO_Fun_.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
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
    public IActionResult Create() => View(new ProblemEditVm());

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
                Description = vm.Description ?? "",
                Difficulty = vm.Difficulty ?? "Easy",
                Tags = vm.Tags ?? "",
                CodeTemplatesJson = vm.CodeTemplatesJson ?? "{}",
                OrderIndex = vm.OrderIndex
            };
            _db.Problems.Add(problem);
            await _db.SaveChangesAsync(ct);

            if (vm.InFiles?.Any() == true && vm.OutFiles?.Any() == true)
                await SaveTestFilesAsync(problem.Id, vm.InFiles, vm.OutFiles, ct);

            return RedirectToAction(nameof(Index));
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

        var vm = new ProblemEditVm
        {
            Id = p.Id,
            Title = p.Title,
            Slug = p.Slug,
            Description = p.Description,
            Difficulty = p.Difficulty,
            Tags = p.Tags,
            CodeTemplatesJson = p.CodeTemplatesJson,
            OrderIndex = p.OrderIndex,
            ExistingTestCount = p.Tests.Count
        };
        return View(vm);
    }

    [HttpPost("Edit/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ProblemEditVm vm, CancellationToken ct)
    {
        if (id != vm.Id) return BadRequest();
        var existing = await _db.Problems
            .Include(x => x.Tests)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (existing == null) return NotFound();

        if (await _db.Problems.AnyAsync(p => p.Slug == vm.Slug && p.Id != id, ct))
            ModelState.AddModelError(nameof(vm.Slug), "A problem with this slug already exists.");

        if (ModelState.IsValid)
        {
            existing.Title = vm.Title;
            existing.Slug = vm.Slug;
            existing.Description = vm.Description ?? "";
            existing.Difficulty = vm.Difficulty ?? "Easy";
            existing.Tags = vm.Tags ?? "";
            existing.CodeTemplatesJson = vm.CodeTemplatesJson ?? "{}";
            existing.OrderIndex = vm.OrderIndex;

            if (vm.InFiles?.Any() == true && vm.OutFiles?.Any() == true)
                await SaveTestFilesAsync(existing.Id, vm.InFiles, vm.OutFiles, ct);

            await _db.SaveChangesAsync(ct);
            return RedirectToAction(nameof(Index));
        }
        vm.ExistingTestCount = existing.Tests.Count;
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

    private async Task SaveTestFilesAsync(int problemId, List<IFormFile> inFiles, List<IFormFile> outFiles, CancellationToken ct)
    {
        var tests = _db.ProblemTests.Where(t => t.ProblemId == problemId).ToList();
        var maxOrder = tests.Count > 0 ? tests.Max(t => t.OrderIndex) : 0;

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

    public string CodeTemplatesJson { get; set; } = "{}";
    public int OrderIndex { get; set; }

    public List<IFormFile>? InFiles { get; set; }
    public List<IFormFile>? OutFiles { get; set; }
    public int ExistingTestCount { get; set; }
}
