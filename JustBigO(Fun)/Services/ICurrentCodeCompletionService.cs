using JustBigO_Fun_.Models;

namespace JustBigO_Fun_.Services;

public sealed record CompleteCurrentCodeResult(
    string Code,
    string ApproachSummary,
    bool TestsPassed,
    SubmissionStatus LastStatus,
    string ResultsJson,
    string? Message);

/// <summary>
/// US 14–16: Orchestrates approach analysis, mentor completion, and local test validation.
/// </summary>
public interface ICurrentCodeCompletionService
{
    Task<CompleteCurrentCodeResult> CompleteAsync(
        int problemId,
        string partialCode,
        string language,
        CancellationToken cancellationToken = default);
}
