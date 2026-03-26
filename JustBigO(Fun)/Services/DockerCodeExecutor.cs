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
        if (Directory.Exists(workDir)) Directory.Delete(workDir, true);
        Directory.CreateDirectory(workDir);

        try
        {
            var results = new List<TestCaseResult>();
            bool allPassed = true;

            foreach (var test in submission.Problem.Tests.OrderBy(t => t.OrderIndex))
            {
                var testResult = await RunTestCaseAsync(submission, test, workDir);
                results.Add(testResult);

                if (testResult.Status != SubmissionStatus.Accepted)
                {
                    allPassed = false;
                    break;
                }
            }

            submission.ResultsJson = JsonSerializer.Serialize(results);
            submission.Status = allPassed ? SubmissionStatus.Accepted : (results.Any() ? results.Last().Status : SubmissionStatus.SystemError);
            submission.ExecutionTimeMs = results.Any(r => r.Status == SubmissionStatus.Accepted) ? results.Where(r => r.Status == SubmissionStatus.Accepted).Max(r => r.TimeMs) : 0;
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
        return path.Replace("\\", "/");
    }

    private async Task<TestCaseResult> RunTestCaseAsync(Submission submission, ProblemTest test, string workDir)
    {
        var result = new TestCaseResult { TestId = test.Id, Input = test.InputJson, Expected = test.ExpectedOutputJson.Trim() };
        var lang = submission.Language.ToLower();
        var methodName = submission.Problem.MethodName ?? "solve";
        
        // 1. Prepare files
        await File.WriteAllTextAsync(Path.Combine(workDir, GetFileName(lang)), submission.SourceCode);
        await File.WriteAllTextAsync(Path.Combine(workDir, "input.json"), test.InputJson);

        string runCmd = "";
        string compileCmd = "";

        if (lang == "python")
        {
            var driver = $@"
import json
import sys
import os

# Add /app to path to find solution.py
sys.path.append('/app')

try:
    import solution
    with open('/app/input.json', 'r') as f:
        data = json.load(f)
    
    func = getattr(solution, '{methodName}')
    if isinstance(data, dict):
        res = func(**data)
    else:
        res = func(data)
    print(json.dumps(res))
except Exception as e:
    import traceback
    traceback.print_exc(file=sys.stderr)
    sys.exit(1)
";
            await File.WriteAllTextAsync(Path.Combine(workDir, "driver.py"), driver);
            runCmd = "python /app/driver.py";
        }
        else if (lang == "java")
        {
            var driver = $@"
import java.util.*;
import java.io.*;

public class Driver {{
    public static void main(String[] args) throws Exception {{
        // Basic placeholder for Java
        Solution sol = new Solution();
        System.err.println(""Java driver JSON parsing not fully implemented yet"");
        System.exit(1);
    }}
}}";
            await File.WriteAllTextAsync(Path.Combine(workDir, "Driver.java"), driver);
            compileCmd = "javac /app/Solution.java /app/Driver.java";
            runCmd = "java -cp /app Driver";
        }
        else if (lang == "cpp")
        {
            compileCmd = "g++ -O3 /app/solution.cpp -o /app/solution_bin";
            runCmd = "/app/solution_bin < /app/input.json";
        }

        var dockerWorkDir = GetDockerPath(workDir);

        // 2. Compile
        if (!string.IsNullOrEmpty(compileCmd))
        {
            var compilePsi = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"run --rm -v \"{dockerWorkDir}:/app\" {GetDockerImage(lang)} sh -c \"cd /app && {compileCmd}\"",
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(compilePsi);
            if (p != null)
            {
                var err = await p.StandardError.ReadToEndAsync();
                await p.WaitForExitAsync();
                if (p.ExitCode != 0)
                {
                    result.Status = SubmissionStatus.CompilationError;
                    result.Error = err;
                    return result;
                }
            }
        }

        // 3. Run
        var stopwatch = Stopwatch.StartNew();
        var runPsi = new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = $"run --rm --network none -v \"{dockerWorkDir}:/app\" --memory 128m --cpus 0.5 {GetDockerImage(lang)} sh -c \"cd /app && {runCmd}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            using var p = Process.Start(runPsi);
            if (p == null) throw new Exception("Failed to start docker.");

            var outTask = p.StandardOutput.ReadToEndAsync();
            var errTask = p.StandardError.ReadToEndAsync();

            if (await Task.WhenAny(Task.Delay(TimeSpan.FromSeconds(5)), p.WaitForExitAsync()) == Task.Delay(TimeSpan.FromSeconds(5)))
            {
                p.Kill(true);
                result.Status = SubmissionStatus.TimeLimitExceeded;
                return result;
            }

            stopwatch.Stop();
            result.TimeMs = stopwatch.Elapsed.TotalMilliseconds;

            var stdout = (await outTask).Trim();
            var stderr = await errTask;

            if (p.ExitCode != 0)
            {
                result.Status = SubmissionStatus.RuntimeError;
                result.Error = string.IsNullOrWhiteSpace(stderr) ? $"Exit Code {p.ExitCode}. Output: {stdout}" : stderr;
                return result;
            }

            if (stdout == result.Expected)
            {
                result.Status = SubmissionStatus.Accepted;
            }
            else
            {
                result.Status = SubmissionStatus.WrongAnswer;
                result.Output = stdout;
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
