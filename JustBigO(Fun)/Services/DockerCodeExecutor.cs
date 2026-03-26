using JustBigO_Fun_.Data;
using JustBigO_Fun_.Models;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Text.Json;
using System.Text;

namespace JustBigO_Fun_.Services;

public class DockerCodeExecutor : ICodeExecutor
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DockerCodeExecutor> _logger;
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

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
        if (Directory.Exists(workDir)) Directory.Delete(workDir, true);
        Directory.CreateDirectory(workDir);

        try
        {
            var results = new List<TestCaseResult>();
            var tests = submission.Problem.Tests.OrderBy(t => t.OrderIndex).ToList();
            
            if (!tests.Any())
            {
                submission.Status = SubmissionStatus.SystemError;
                submission.ErrorMessage = "No test cases found for this problem.";
            }
            else
            {
                foreach (var test in tests)
                {
                    var testResult = await RunTestCaseAsync(submission, test, workDir);
                    results.Add(testResult);
                }

                submission.ResultsJson = JsonSerializer.Serialize(results, JsonOptions);
                submission.Status = results.All(r => r.Status == SubmissionStatus.Accepted) ? SubmissionStatus.Accepted : SubmissionStatus.WrongAnswer;
            }
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

    private async Task<TestCaseResult> RunTestCaseAsync(Submission submission, ProblemTest test, string workDir)
    {
        var result = new TestCaseResult { 
            TestId = test.Id, 
            Input = test.InputJson, 
            Expected = test.ExpectedOutputJson.Trim() 
        };
        
        var lang = submission.Language.ToLower();
        var fileName = lang switch {
            "python" => "solution.py",
            "java" => "Solution.java",
            "cpp" => "solution.cpp",
            _ => "solution.txt"
        };
        
        await File.WriteAllTextAsync(Path.Combine(workDir, fileName), submission.SourceCode, Utf8NoBom);
        await File.WriteAllTextAsync(Path.Combine(workDir, "input.json"), test.InputJson, Utf8NoBom);

        string runCmd = lang switch {
            "python" => $"python /app/{fileName} < /app/input.json",
            "cpp" => $"g++ /app/solution.cpp -o /app/out && /app/out < /app/input.json",
            "java" => $"javac /app/Solution.java && java -cp /app Solution < /app/input.json",
            _ => "cat /app/input.json"
        };

        var dockerWorkDir = workDir.Replace("\\", "/");
        var psi = new ProcessStartInfo {
            FileName = "docker",
            Arguments = $"run --rm -i -v \"{dockerWorkDir}:/app\" {GetDockerImage(lang)} sh -c \"cd /app && {runCmd}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try {
            using var p = Process.Start(psi);
            if (p == null) throw new Exception("Failed to start Docker.");

            var outTask = p.StandardOutput.ReadToEndAsync();
            var errTask = p.StandardError.ReadToEndAsync();
            
            if (await Task.WhenAny(Task.Delay(TimeSpan.FromSeconds(5)), p.WaitForExitAsync()) == Task.Delay(TimeSpan.FromSeconds(5)))
            {
                p.Kill(true);
                result.Status = SubmissionStatus.TimeLimitExceeded;
                result.Error = "Time Limit Exceeded (5s)";
                return result;
            }

            result.Output = (await outTask).Trim();
            result.Error = await errTask;
            
            if (p.ExitCode != 0)
            {
                result.Status = SubmissionStatus.RuntimeError;
                if (string.IsNullOrWhiteSpace(result.Error)) result.Error = $"Exit Code {p.ExitCode}";
            }
            else
            {
                result.Status = (result.Output == result.Expected) ? SubmissionStatus.Accepted : SubmissionStatus.WrongAnswer;
            }
        } catch (Exception ex) {
            result.Error = ex.Message;
            result.Status = SubmissionStatus.SystemError;
        }

        return result;
    }

    private string GetDockerImage(string language) => language.ToLower() switch {
        "python" => "python:3.10-slim",
        "java" => "openjdk:17-slim",
        "cpp" => "gcc:12",
        _ => "alpine"
    };
}

public class TestCaseResult
{
    public int TestId { get; set; }
    public SubmissionStatus Status { get; set; }
    public string? Input { get; set; }
    public string? Expected { get; set; }
    public string? Output { get; set; }
    public string? Error { get; set; }
}
