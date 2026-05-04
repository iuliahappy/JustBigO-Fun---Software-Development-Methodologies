using JustBigO_Fun_.Models;

namespace JustBigO_Fun_.Services;

public interface ICodeExecutor
{
    Task ExecuteAsync(int submissionId);

    /// <summary>
    /// Runs compile + tests for the given code without persisting a submission (for AI completion validation).
    /// </summary>
    Task<(SubmissionStatus Status, string ResultsJson, string? ErrorMessage)> EvaluateCodeAsync(
        int problemId,
        string sourceCode,
        string language,
        CancellationToken cancellationToken = default);
}
