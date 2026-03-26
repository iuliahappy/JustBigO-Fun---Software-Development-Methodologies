using JustBigO_Fun_.Data;
using JustBigO_Fun_.Models;
using JustBigO_Fun_.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace JustBigO_Fun_.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SubmissionController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ICodeExecutor _executor;

    public SubmissionController(ApplicationDbContext db, ICodeExecutor executor)
    {
        _db = db;
        _executor = executor;
    }

    [HttpPost]
    public async Task<IActionResult> Submit([FromBody] SubmissionRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.SourceCode))
        {
            return BadRequest("Source code is required.");
        }

        var problem = await _db.Problems
            .Include(p => p.Tests)
            .FirstOrDefaultAsync(p => p.Id == request.ProblemId);

        if (problem == null)
        {
            return NotFound("Problem not found.");
        }

        var submission = new Submission
        {
            ProblemId = request.ProblemId,
            SourceCode = request.SourceCode,
            Language = request.Language,
            Status = SubmissionStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
        };

        _db.Submissions.Add(submission);
        await _db.SaveChangesAsync();

        // Start Docker execution here in a fire-and-forget way for this prototype.
        _ = Task.Run(() => _executor.ExecuteAsync(submission.Id));
        
        return Ok(new { submissionId = submission.Id, status = submission.Status.ToString() });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetStatus(int id)
    {
        var submission = await _db.Submissions
            .Include(s => s.Problem)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (submission == null)
        {
            return NotFound();
        }

        return Ok(new
        {
            id = submission.Id,
            status = submission.Status.ToString(),
            results = submission.ResultsJson,
            executionTimeMs = submission.ExecutionTimeMs,
            errorMessage = submission.ErrorMessage,
            problemMethodName = submission.Problem?.MethodName
        });
    }
}

public class SubmissionRequest
{
    public int ProblemId { get; set; }
    public string SourceCode { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
}
