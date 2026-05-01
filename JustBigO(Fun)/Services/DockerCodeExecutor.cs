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
            // 1. Prepare solution files and Compile
            var compileResult = await PrepareAndCompileAsync(submission, workDir);
            if (!compileResult.Success)
            {
                submission.Status = SubmissionStatus.CompilationError;
                submission.ErrorMessage = compileResult.Error;
                await db.SaveChangesAsync();
                return;
            }

            // 2. Run Test Cases
            var results = new List<TestCaseResult>();
            foreach (var test in submission.Problem.Tests.OrderBy(t => t.OrderIndex))
            {
                var testResult = await RunTestCaseAsync(submission, test, workDir);
                results.Add(testResult);
                
                // Optional: Stop on first critical error (TLE, MLE, RE)
                if (testResult.Status != SubmissionStatus.Accepted && testResult.Status != SubmissionStatus.WrongAnswer)
                {
                    // But for now, let's continue to get full results
                }
            }

            submission.ResultsJson = JsonSerializer.Serialize(results, JsonOptions);
            
            // 3. Aggregate Status
            submission.Status = AggregateStatus(results);
            
            // If any test has a specific error, we might want to put it in ErrorMessage if not already set
            if (submission.Status != SubmissionStatus.Accepted && submission.Status != SubmissionStatus.WrongAnswer)
            {
                var firstError = results.FirstOrDefault(r => r.Status == submission.Status);
                submission.ErrorMessage = firstError?.Error;
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

    private SubmissionStatus AggregateStatus(List<TestCaseResult> results)
    {
        if (results.Count == 0) return SubmissionStatus.Accepted;

        // Priority order for reporting
        var priorities = new[] {
            SubmissionStatus.CompilationError,
            SubmissionStatus.TimeLimitExceeded,
            SubmissionStatus.MemoryLimitExceeded,
            SubmissionStatus.RuntimeError,
            SubmissionStatus.WrongAnswer,
            SubmissionStatus.SystemError
        };

        foreach (var status in priorities)
        {
            if (results.Any(r => r.Status == status)) return status;
        }

        return SubmissionStatus.Accepted;
    }

    private async Task<(bool Success, string? Error)> PrepareAndCompileAsync(Submission submission, string workDir)
    {
        var lang = submission.Language.ToLower();
        var snakeMethod = submission.Problem.MethodName ?? "solve";
        var camelMethod = ToCamelCase(snakeMethod);

        if (lang == "python")
        {
            await File.WriteAllTextAsync(Path.Combine(workDir, "solution.py"), submission.SourceCode, Utf8NoBom);
            var driver = $@"
import json
import sys
import solution
try:
    with open('/app/input.json', 'r') as f: data = json.load(f)
    func = getattr(solution, '{snakeMethod}', getattr(solution, '{camelMethod}', None))
    if func is None:
        print(f'Error: Method {snakeMethod} or {camelMethod} not found', file=sys.stderr)
        sys.exit(1)
    res = func(**data) if isinstance(data, dict) else func(data)
    print(json.dumps(res))
except Exception as e:
    import traceback
    traceback.print_exc(file=sys.stderr)
    sys.exit(1)
";
            await File.WriteAllTextAsync(Path.Combine(workDir, "driver.py"), driver, Utf8NoBom);
            
            // Step A: Statically check for syntax errors
            var checkResult = await RunDockerCommandAsync(workDir, "python -m py_compile solution.py", "python", 10);
            if (checkResult.ExitCode != 0)
            {
                return (false, checkResult.Error);
            }
            
            return (true, null);
        }
        else if (lang == "cpp")
        {
            await File.WriteAllTextAsync(Path.Combine(workDir, "solution.cpp"), submission.SourceCode, Utf8NoBom);
            var driver = $@"
#include <iostream>
#include <vector>
#include <string>
#include <fstream>
#include <nlohmann/json.hpp>
#include ""solution.cpp""

using json = nlohmann::json;

int main() {{
    try {{
        Solution sol;
        std::ifstream f(""/app/input.json"");
        json data = json::parse(f);
        
        // This is still hardcoded for Two Sum prototype, but let's keep it for now
        // A real system would generate the driver based on problem metadata
        std::vector<int> nums = data[""nums""].get<std::vector<int>>();
        int target = data[""target""].get<int>();
        
        std::vector<int> res = sol.{camelMethod}(nums, target);
        
        std::cout << json(res).dump() << std::endl;
    }} catch (const std::exception& e) {{
        std::cerr << e.what() << std::endl;
        return 1;
    }}
    return 0;
}}";
            await File.WriteAllTextAsync(Path.Combine(workDir, "driver.cpp"), driver, Utf8NoBom);
            
            var compileCmd = "LC_ALL=C g++ -O3 -I/usr/include /app/driver.cpp -o /app/out";
            var result = await RunDockerCommandAsync(workDir, compileCmd, "cpp", 30);
            
            if (result.ExitCode != 0)
            {
                return (false, result.Error);
            }
            return (true, null);
        }
        else if (lang == "java")
        {
            await File.WriteAllTextAsync(Path.Combine(workDir, "Solution.java"), submission.SourceCode, Utf8NoBom);
            var driver = $@"
import java.util.*;
import java.io.*;
import java.nio.file.*;

public class Driver {{
    public static void main(String[] args) throws Exception {{
        try {{
            String content = new String(Files.readAllBytes(Paths.get(""/app/input.json"")));
            // Minimal manual parsing for prototype
            int startBracket = content.indexOf(""["");
            int endBracket = content.indexOf(""]"");
            String numsStr = content.substring(startBracket + 1, endBracket);
            int target = Integer.parseInt(content.substring(content.lastIndexOf("":"") + 1).replace(""}}"", """").trim());
            
            int[] nums = Arrays.stream(numsStr.split("","")).map(String::trim).filter(s -> !s.isEmpty()).mapToInt(Integer::parseInt).toArray();
            
            Solution sol = new Solution();
            int[] res = sol.{camelMethod}(nums, target);
            System.out.println(Arrays.toString(res).replace("" "", """"));
        }} catch (Exception e) {{
            e.printStackTrace();
            System.exit(1);
        }}
    }}
}}";
            await File.WriteAllTextAsync(Path.Combine(workDir, "Driver.java"), driver, Utf8NoBom);
            
            var compileCmd = "javac /app/Solution.java /app/Driver.java";
            var result = await RunDockerCommandAsync(workDir, compileCmd, "java", 30);
            
            if (result.ExitCode != 0)
            {
                return (false, result.Error);
            }
            return (true, null);
        }

        return (false, $"Unsupported language: {lang}");
    }

    private async Task<TestCaseResult> RunTestCaseAsync(Submission submission, ProblemTest test, string workDir)
    {
        var result = new TestCaseResult { TestId = test.Id, Input = test.InputJson, Expected = test.ExpectedOutputJson.Trim() };
        var lang = submission.Language.ToLower();
        
        await File.WriteAllTextAsync(Path.Combine(workDir, "input.json"), test.InputJson, Utf8NoBom);

        string runCmd = lang switch
        {
            "python" => "python /app/driver.py",
            "cpp" => "/app/out",
            "java" => "java -cp /app Driver",
            _ => throw new Exception("Unsupported language")
        };

        // Use 'timeout' command inside docker for better TLE control
        var timedCmd = $"timeout 5s {runCmd}";

        var dockerResult = await RunDockerCommandAsync(workDir, timedCmd, lang, 7); // 7s total timeout for the process

        if (dockerResult.ExitCode == 124) // timeout command exit code
        {
            result.Status = SubmissionStatus.TimeLimitExceeded;
            result.Error = "Time Limit Exceeded (5s)";
            result.Output = ""; // Suppress output for TLE
        }
        else if (dockerResult.ExitCode == 137) // OOM or killed
        {
            result.Status = SubmissionStatus.MemoryLimitExceeded;
            result.Error = "Memory Limit Exceeded (256MB)";
            result.Output = ""; // Suppress output for MLE
        }
        else if (dockerResult.ExitCode != 0)
        {
            result.Status = SubmissionStatus.RuntimeError;
            result.Error = dockerResult.Error;
            result.Output = ""; // Suppress output for RE
        }
        else
        {
            result.Output = dockerResult.Output.Trim();
            result.Status = IsMatch(result.Output, result.Expected) ? SubmissionStatus.Accepted : SubmissionStatus.WrongAnswer;
        }

        return result;
    }

    private async Task<(int ExitCode, string Output, string Error)> RunDockerCommandAsync(string workDir, string cmd, string lang, int timeoutSeconds)
    {
        var dockerWorkDir = workDir.Replace("\\", "/");
        var psi = new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = $"run --rm --network none -m 256m --cpus=\"1.0\" -v \"{dockerWorkDir}:/app\" {GetDockerImage(lang)} sh -c \"cd /app && {cmd}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var p = Process.Start(psi);
        if (p == null) throw new Exception("Failed to start docker process.");

        var outTask = p.StandardOutput.ReadToEndAsync();
        var errTask = p.StandardError.ReadToEndAsync();

        if (await Task.WhenAny(Task.Delay(TimeSpan.FromSeconds(timeoutSeconds)), p.WaitForExitAsync()) == Task.Delay(TimeSpan.FromSeconds(timeoutSeconds)))
        {
            p.Kill(true);
            // Try to kill the container if it's still running? 
            // In a production system, we'd use --name and docker kill, but here we hope --rm and p.Kill(true) are enough.
            return (124, "", "Timed out waiting for Docker client");
        }

        return (p.ExitCode, await outTask, await errTask);
    }

    // ADD THIS NEW METHOD FOR THE AI
    public async Task<(bool IsSuccess, string ErrorMessage)> TestRawCodeAsync(string sourceCode, string language)
    {
        // Create a unique temporary folder just for this AI test
        var workDir = Path.Combine(Path.GetTempPath(), "justbigo_ai", Guid.NewGuid().ToString());
        Directory.CreateDirectory(workDir);

        try
        {
            string cmd = "";

            if (language == "python")
            {
                await File.WriteAllTextAsync(Path.Combine(workDir, "solution.py"), sourceCode, Utf8NoBom);
                cmd = "python -m py_compile solution.py"; // Check Python syntax
            }
            else if (language == "cpp")
            {
                await File.WriteAllTextAsync(Path.Combine(workDir, "solution.cpp"), sourceCode, Utf8NoBom);
                cmd = "LC_ALL=C g++ -c solution.cpp -o /dev/null"; // Compile C++ without linking
            }
            else if (language == "java")
            {
                // Find the class name the AI generated (usually Main or Solution) so we can name the file correctly
                var classNameMatch = Regex.Match(sourceCode, @"class\s+([A-Za-z0-9_]+)");
                var className = classNameMatch.Success ? classNameMatch.Groups[1].Value : "Solution";

                await File.WriteAllTextAsync(Path.Combine(workDir, $"{className}.java"), sourceCode, Utf8NoBom);
                cmd = $"javac {className}.java"; // Compile Java
            }
            else
            {
                return (false, "Unsupported language.");
            }

            // Run it in the Sandbox with a 10-second timeout
            var result = await RunDockerCommandAsync(workDir, cmd, language, 10);

            if (result.ExitCode != 0)
            {
                // If it failed, return the exact compiler error to the AI!
                string errorMsg = string.IsNullOrWhiteSpace(result.Error) ? result.Output : result.Error;
                return (false, errorMsg);
            }

            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI TestRawCodeAsync failed internally.");
            return (false, ex.Message);
        }
        finally
        {
            // Always clean up the temporary folder!
            try { Directory.Delete(workDir, true); } catch { }
        }
    }
    private string ToCamelCase(string snakeCase) => Regex.Replace(snakeCase, "_([a-z])", m => m.Groups[1].Value.ToUpper());

    private bool IsMatch(string actual, string expected) => actual.Replace(" ", "") == expected.Replace(" ", "");

    private string GetDockerImage(string language) => "justbigo-runner:latest";
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
