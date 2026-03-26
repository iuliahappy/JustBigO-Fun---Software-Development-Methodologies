using JustBigO_Fun_.Data;
using JustBigO_Fun_.Models;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Text.Json;

namespace JustBigO_Fun_.Services;

public class DockerCodeExecutor : ICodeExecutor
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DockerCodeExecutor> _logger;

    public DockerCodeExecutor(IServiceScopeFactory scopeFactory, ILogger<DockerCodeExecutor> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task ExecuteAsync(int submissionId)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var submission = await db.Submissions
            .Include(s => s.Problem)
                .ThenInclude(p => p.Tests)
            .FirstOrDefaultAsync(s => s.Id == submissionId);

        if (submission == null) return;

        submission.Status = SubmissionStatus.Running;
        await db.SaveChangesAsync();

        var workDir = Path.Combine(Path.GetTempPath(), "justbigo", submission.Id.ToString());
        Directory.CreateDirectory(workDir);

        try
        {
            var fileName = GetFileName(submission.Language);
            var filePath = Path.Combine(workDir, fileName);
            await File.WriteAllTextAsync(filePath, submission.SourceCode);

            var results = new List<TestCaseResult>();
            bool allPassed = true;

            foreach (var test in submission.Problem.Tests.OrderBy(t => t.OrderIndex))
            {
                var testResult = await RunTestCaseAsync(submission, test, workDir, fileName);
                results.Add(testResult);

                if (testResult.Status != SubmissionStatus.Accepted)
                {
                    allPassed = false;
                    // In some platforms, they stop at first failure. Let's do that for now.
                    break;
                }
            }

            submission.ResultsJson = JsonSerializer.Serialize(results);
            submission.Status = allPassed ? SubmissionStatus.Accepted : results.Last().Status;
            submission.ExecutionTimeMs = results.Any() ? results.Max(r => r.TimeMs) : 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing submission {SubmissionId}", submission.Id);
            submission.Status = SubmissionStatus.SystemError;
            submission.ErrorMessage = ex.Message;
        }
        finally
        {
            try { Directory.Delete(workDir, true); } catch { }
            await db.SaveChangesAsync();
        }
    }

    private string GetFileName(string language) => language.ToLower() switch
    {
        "python" => "solution.py",
        "java" => "Solution.java",
        "cpp" => "solution.cpp",
        _ => "solution.txt"
    };

    private string GetDockerImage(string language) => language.ToLower() switch
    {
        "python" => "python:3.10-slim",
        "java" => "openjdk:17-slim",
        "cpp" => "gcc:12",
        _ => "alpine"
    };

    private async Task<TestCaseResult> RunTestCaseAsync(Submission submission, ProblemTest test, string workDir, string fileName)
    {
        var result = new TestCaseResult { TestId = test.Id };
        var inputPath = Path.Combine(workDir, $"test_{test.Id}.in");
        await File.WriteAllTextAsync(inputPath, test.InputJson);

        var stopwatch = Stopwatch.StartNew();
        
        // 1. Compile if necessary
        if (submission.Language.ToLower() is "java" or "cpp")
        {
            var compileCmd = submission.Language.ToLower() switch
            {
                "java" => "javac /app/Solution.java",
                "cpp" => "g++ -O3 /app/solution.cpp -o /app/solution",
                _ => ""
            };

            var compilePsi = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"run --rm -v \"{workDir}:/app\" {GetDockerImage(submission.Language)} sh -c \"{compileCmd}\"",
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var compileProcess = Process.Start(compilePsi);
            if (compileProcess != null)
            {
                var error = await compileProcess.StandardError.ReadToEndAsync();
                await compileProcess.WaitForExitAsync();
                if (compileProcess.ExitCode != 0)
                {
                    result.Status = SubmissionStatus.CompilationError;
                    result.Error = error;
                    return result;
                }
            }
        }

        // 2. Run
        string runCommand = submission.Language.ToLower() switch
        {
            "python" => $"python /app/{fileName} < /app/test_{test.Id}.in",
            "java" => $"java -cp /app Solution < /app/test_{test.Id}.in",
            "cpp" => $"/app/solution < /app/test_{test.Id}.in",
            _ => "cat"
        };

        var psi = new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = $"run --rm --network none -v \"{workDir}:/app\" --memory 128m --cpus 0.5 {GetDockerImage(submission.Language)} sh -c \"{runCommand}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            using var process = Process.Start(psi);
            if (process == null) throw new Exception("Failed to start docker process.");

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            if (await Task.WhenAny(Task.Delay(TimeSpan.FromSeconds(5)), process.WaitForExitAsync()) == Task.Delay(TimeSpan.FromSeconds(5)))
            {
                process.Kill(true);
                result.Status = SubmissionStatus.TimeLimitExceeded;
                return result;
            }

            stopwatch.Stop();
            result.TimeMs = stopwatch.Elapsed.TotalMilliseconds;

            var output = (await outputTask).Trim();
            var error = await errorTask;

            if (process.ExitCode != 0)
            {
                result.Status = SubmissionStatus.RuntimeError;
                result.Error = error;
                return result;
            }

            // Compare output
            var expected = test.ExpectedOutputJson.Trim();
            if (output == expected)
            {
                result.Status = SubmissionStatus.Accepted;
            }
            else
            {
                result.Status = SubmissionStatus.WrongAnswer;
                result.Output = output;
            }
        }
        catch (Exception ex)
        {
            result.Status = SubmissionStatus.SystemError;
            result.Error = ex.Message;
        }

        return result;
    }
}

public class TestCaseResult
{
    public int TestId { get; set; }
    public SubmissionStatus Status { get; set; }
    public double TimeMs { get; set; }
    public string? Output { get; set; }
    public string? Error { get; set; }
}
