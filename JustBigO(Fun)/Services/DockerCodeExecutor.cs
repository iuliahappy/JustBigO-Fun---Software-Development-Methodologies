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

    private string GetDockerPath(string path)
    {
        // On Windows, docker volumes need a specific format or it might fail depending on the setup.
        // Usually "C:\path" works, but sometimes it needs to be normalized.
        return path.Replace("\\", "/");
    }

    private async Task<TestCaseResult> RunTestCaseAsync(Submission submission, ProblemTest test, string workDir, string fileName)
    {
        var result = new TestCaseResult { TestId = test.Id };
        
        // Prepare code with driver if needed
        string finalFileName = fileName;
        if (submission.Language.ToLower() == "python")
        {
            var methodName = submission.Problem.MethodName ?? "solve";
            var driverCode = $@"
import json
import sys

# User Solution
{submission.SourceCode}

if __name__ == ""__main__"":
    try:
        line = sys.stdin.read()
        if not line:
            sys.exit(0)
        data = json.loads(line)
        
        # Call the method with arguments from JSON
        if isinstance(data, dict):
            res = {methodName}(**data)
        else:
            res = {methodName}(data)
            
        print(json.dumps(res))
    except Exception as e:
        print(e, file=sys.stderr)
        sys.exit(1)
";
            finalFileName = "driver.py";
            await File.WriteAllTextAsync(Path.Combine(workDir, finalFileName), driverCode);
        }
        else
        {
            await File.WriteAllTextAsync(Path.Combine(workDir, fileName), submission.SourceCode);
        }

        var inputPath = Path.Combine(workDir, $"test_{test.Id}.in");
        await File.WriteAllTextAsync(inputPath, test.InputJson);

        var dockerWorkDir = GetDockerPath(workDir);
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
                Arguments = $"run --rm -v \"{dockerWorkDir}:/app\" {GetDockerImage(submission.Language)} sh -c \"{compileCmd}\"",
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
            "python" => $"python /app/{finalFileName} < /app/test_{test.Id}.in",
            "java" => $"java -cp /app Solution < /app/test_{test.Id}.in",
            "cpp" => $"/app/solution < /app/test_{test.Id}.in",
            _ => "cat"
        };

        var psi = new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = $"run --rm --network none -v \"{dockerWorkDir}:/app\" --memory 128m --cpus 0.5 {GetDockerImage(submission.Language)} sh -c \"{runCommand}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            using var process = Process.Start(psi);
            if (process == null) 
            {
                result.Status = SubmissionStatus.SystemError;
                result.Error = "Failed to start docker process (Process.Start returned null).";
                return result;
            }

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
                result.Error = string.IsNullOrWhiteSpace(error) ? $"Process exited with code {process.ExitCode}" : error;
                _logger.LogWarning("Docker execution failed for submission {SubmissionId}. ExitCode: {ExitCode}, Error: {Error}", submission.Id, process.ExitCode, error);
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
                result.Input = test.InputJson;
                result.Expected = expected;
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
    public string? Input { get; set; }
    public string? Expected { get; set; }
    public string? Output { get; set; }
    public string? Error { get; set; }
}
