namespace JustBigO_Fun_.Services;

/// <summary>
/// US 16: Mentor agent — completes code continuing the approach described by the analyzer.
/// </summary>
public interface ICodeCompleter
{
    Task<string> GenerateCompletionAsync(
        string problemTitle,
        string problemDescription,
        string? methodName,
        string currentCode,
        string language,
        string approachSummary,
        string? testFailureFeedback,
        CancellationToken cancellationToken = default);
}
