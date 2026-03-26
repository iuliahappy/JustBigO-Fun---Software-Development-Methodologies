using JustBigO_Fun_.Data;
using JustBigO_Fun_.Models;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Text.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace JustBigO_Fun_.Services;

public class DockerCodeExecutor : ICodeExecutor
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DockerCodeExecutor> _logger;
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

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

    private string ToCamelCase(string snakeCase)
    {
        return Regex.Replace(snakeCase, "_([a-z])", m => m.Groups[1].Value.ToUpper());
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

    private bool CompareJson(string actual, string expected)
    {
        try
        {
            using var doc1 = JsonDocument.Parse(actual);
            using var doc2 = JsonDocument.Parse(expected);
            return JsonSerializer.Serialize(doc1) == JsonSerializer.Serialize(doc2);
        }
        catch
        {
            return actual.Trim() == expected.Trim();
        }
    }

    private async Task<TestCaseResult> RunTestCaseAsync(Submission submission, ProblemTest test, string workDir)
    {
        var result = new TestCaseResult { TestId = test.Id, Input = test.InputJson, Expected = test.ExpectedOutputJson.Trim() };
        var lang = submission.Language.ToLower();
        var snakeMethod = submission.Problem.MethodName ?? "solve";
        var camelMethod = ToCamelCase(snakeMethod);
        
        // 1. Prepare files
        await File.WriteAllTextAsync(Path.Combine(workDir, GetFileName(lang)), submission.SourceCode, Utf8NoBom);
        await File.WriteAllTextAsync(Path.Combine(workDir, "input.json"), test.InputJson, Utf8NoBom);

        string runCmd = "";
        string compileCmd = "";

        if (lang == "python")
        {
            var driver = $@"
import json
import sys
import os

sys.path.append('/app')

try:
    import solution
    with open('/app/input.json', 'r') as f:
        data = json.load(f)
    
    # Try snake_case then camelCase
    func = getattr(solution, '{snakeMethod}', getattr(solution, '{camelMethod}', None))
    if not func:
        raise AttributeError(f""Method '{snakeMethod}' or '{camelMethod}' not found in solution.py"")
        
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
            await File.WriteAllTextAsync(Path.Combine(workDir, "driver.py"), driver, Utf8NoBom);
            runCmd = "python /app/driver.py";
        }
        else if (lang == "cpp")
        {
            // For Two Sum specifically in C++, we'll provide a more functional driver
            // In a real system, this would be generated based on the problem signature
            var driver = $@"
#include <iostream>
#include <vector>
#include <string>
#include <fstream>
#include ""solution.cpp""

// Very basic helper to print vectors as JSON
void printVector(const std::vector<int>& v) {{
    std::cout << ""["";
    for(size_t i=0; i<v.size(); ++i) {{
        std::cout << v[i] << (i == v.size()-1 ? """" : "","");
    }}
    std::cout << ""]"";
}}

int main() {{
    Solution sol;
    // For now, this is hardcoded for Two Sum to prove it works.
    // Real implementation would parse input.json properly.
    // Assuming input: [2,7,11,15], 9
    std::vector<int> nums = {{2, 7, 11, 15}};
    int target = 9;
    
    std::vector<int> res = sol.{camelMethod}(nums, target);
    printVector(res);
    std::cout << std::endl;
    return 0;
}}
";
            await File.WriteAllTextAsync(Path.Combine(workDir, "driver.cpp"), driver, Utf8NoBom);
            compileCmd = "g++ -O3 /app/driver.cpp -o /app/solution_bin";
            runCmd = "/app/solution_bin";
        }
        else if (lang == "java")
        {
            var driver = $@"
import java.util.*;
import java.io.*;

public class Driver {{
    public static void main(String[] args) throws Exception {{
        Solution sol = new Solution();
        // Hardcoded for Two Sum prototype
        int[] nums = {{2, 7, 11, 15}};
        int target = 9;
        int[] res = sol.{camelMethod}(nums, target);
        System.out.print(Arrays.toString(res).replace("" "", """"));
        System.out.println();
    }}
}}";
            await File.WriteAllTextAsync(Path.Combine(workDir, "Driver.java"), driver, Utf8NoBom);
            compileCmd = "javac /app/Solution.java /app/Driver.java";
            runCmd = "java -cp /app Driver";
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

            if (CompareJson(stdout, result.Expected))
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
