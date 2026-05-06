using JustBigO_Fun_.Data;
using JustBigO_Fun_.Models;
using Microsoft.EntityFrameworkCore;

namespace JustBigO_Fun_.Services;

public class CurrentCodeCompletionService : ICurrentCodeCompletionService
{
    private const int MaxRepairAttempts = 3;
    private const int MaxFeedbackChars = 3500;

    private readonly ApplicationDbContext _db;
    private readonly IApproachAnalyzer _approachAnalyzer;
    private readonly ICodeCompleter _codeCompleter;
    private readonly ICodeExecutor _codeExecutor;
    private readonly ILogger<CurrentCodeCompletionService> _logger;

    public CurrentCodeCompletionService(
        ApplicationDbContext db,
        IApproachAnalyzer approachAnalyzer,
        ICodeCompleter codeCompleter,
        ICodeExecutor codeExecutor,
        ILogger<CurrentCodeCompletionService> logger)
    {
        _db = db;
        _approachAnalyzer = approachAnalyzer;
        _codeCompleter = codeCompleter;
        _codeExecutor = codeExecutor;
        _logger = logger;
    }

    public async Task<CompleteCurrentCodeResult> CompleteAsync(
        int problemId,
        string partialCode,
        string language,
        CancellationToken cancellationToken = default)
    {
        var problem = await _db.Problems
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == problemId, cancellationToken);

        if (problem == null)
        {
            return new CompleteCurrentCodeResult(
                partialCode,
                string.Empty,
                false,
                SubmissionStatus.SystemError,
                "[]",
                "Problem not found.");
        }

        if (string.IsNullOrWhiteSpace(partialCode))
        {
            return new CompleteCurrentCodeResult(
                partialCode,
                string.Empty,
                false,
                SubmissionStatus.SystemError,
                "[]",
                "Editor is empty; nothing to complete.");
        }

        var lang = string.IsNullOrWhiteSpace(language) ? "python" : language.Trim();

        string approachSummary;
        try
        {
            approachSummary = await _approachAnalyzer.DescribeApproachAsync(partialCode, lang, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Approach analysis failed for problem {ProblemId}", problemId);
            approachSummary = "Approach analysis unavailable; completing from problem statement and user code only.";
        }

        string code = partialCode;
        try
        {
            code = await _codeCompleter.GenerateCompletionAsync(
                problem.Title,
                problem.Description,
                problem.MethodName,
                partialCode,
                lang,
                approachSummary,
                null,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Mentor completion failed for problem {ProblemId}", problemId);
            return new CompleteCurrentCodeResult(
                partialCode,
                approachSummary,
                false,
                SubmissionStatus.SystemError,
                "[]",
                "Could not generate completion. Check Gemini API configuration.");
        }

        SubmissionStatus lastStatus = SubmissionStatus.SystemError;
        var resultsJson = "[]";
        string? feedback = null;

        for (var attempt = 0; attempt < MaxRepairAttempts; attempt++)
        {
            try
            {
                var eval = await _codeExecutor.EvaluateCodeAsync(problemId, code, lang, cancellationToken);
                lastStatus = eval.Status;
                resultsJson = eval.ResultsJson;

                if (eval.Status == SubmissionStatus.Accepted)
                {
                    return new CompleteCurrentCodeResult(
                        code,
                        approachSummary,
                        true,
                        lastStatus,
                        resultsJson,
                        null);
                }

                if (attempt == MaxRepairAttempts - 1)
                    break;

                feedback = BuildTestFeedback(eval.Status, eval.ResultsJson, eval.ErrorMessage);
                code = await _codeCompleter.GenerateCompletionAsync(
                    problem.Title,
                    problem.Description,
                    problem.MethodName,
                    code,
                    lang,
                    approachSummary,
                    feedback,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Local test evaluation failed on attempt {Attempt}", attempt + 1);
                lastStatus = SubmissionStatus.SystemError;
                if (attempt == MaxRepairAttempts - 1)
                    break;
                feedback = ex.Message;
                code = await _codeCompleter.GenerateCompletionAsync(
                    problem.Title,
                    problem.Description,
                    problem.MethodName,
                    code,
                    lang,
                    approachSummary,
                    feedback,
                    cancellationToken);
            }
        }

        return new CompleteCurrentCodeResult(
            code,
            approachSummary,
            false,
            lastStatus,
            resultsJson,
            "The AI-filled code did not pass all tests after automatic retries. The editor was still updated with the last attempt — review output below or fix manually, then Submit.");
    }

    private static string BuildTestFeedback(SubmissionStatus status, string resultsJson, string? errorMessage)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Aggregate status: {status}");
        if (!string.IsNullOrWhiteSpace(errorMessage))
        {
            sb.AppendLine("Compiler / runtime message:");
            sb.AppendLine(errorMessage);
        }

        sb.AppendLine("Test results JSON (truncated):");
        var slice = resultsJson.Length <= MaxFeedbackChars
            ? resultsJson
            : resultsJson[..MaxFeedbackChars] + "...";
        sb.Append(slice);
        return sb.ToString();
    }
}
