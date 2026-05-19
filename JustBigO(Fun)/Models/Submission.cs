using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity; // Adăugat pentru IdentityUser

namespace JustBigO_Fun_.Models;

public enum SubmissionStatus
{
    Pending, Compiling, Running, Accepted, WrongAnswer,
    TimeLimitExceeded, MemoryLimitExceeded, RuntimeError,
    CompilationError, SystemError
}

public class Submission
{
    public int Id { get; set; }

    public int ProblemId { get; set; }
    [ForeignKey(nameof(ProblemId))]
    public Problem Problem { get; set; } = null!;

    // --- MODIFICARE: Legătură explicită cu IdentityUser ---
    public string? UserId { get; set; }
    [ForeignKey(nameof(UserId))]
    public virtual IdentityUser? User { get; set; }
    // ------------------------------------------------------

    [Required]
    public string SourceCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Language { get; set; } = string.Empty;

    public SubmissionStatus Status { get; set; } = SubmissionStatus.Pending;

    public string? ResultsJson { get; set; }

    public double? ExecutionTimeMs { get; set; }

    public double? UserTimeMs { get; set; }

    public double? PeakMemoryKb { get; set; }

    public double? MemoryLimitKb { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? ErrorMessage { get; set; }
}