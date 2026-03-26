using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace JustBigO_Fun_.Models;

public enum SubmissionStatus
{
    Pending,
    Compiling,
    Running,
    Accepted,
    WrongAnswer,
    TimeLimitExceeded,
    MemoryLimitExceeded,
    RuntimeError,
    CompilationError,
    SystemError
}

public class Submission
{
    public int Id { get; set; }

    public int ProblemId { get; set; }

    [ForeignKey(nameof(ProblemId))]
    public Problem Problem { get; set; } = null!;

    public string? UserId { get; set; }

    [Required]
    public string SourceCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Language { get; set; } = string.Empty;

    public SubmissionStatus Status { get; set; } = SubmissionStatus.Pending;

    /// <summary>JSON: [{ "testId": 1, "status": "Accepted", "timeMs": 10, "memoryKb": 1024, "output": "...", "error": "..." }]</summary>
    public string? ResultsJson { get; set; }

    public double? ExecutionTimeMs { get; set; }

    public double? MemoryLimitKb { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? ErrorMessage { get; set; }
}
