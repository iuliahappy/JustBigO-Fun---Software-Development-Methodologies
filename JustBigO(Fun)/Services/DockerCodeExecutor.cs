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
            var results = new List<TestCaseResult>();
            foreach (var test in submission.Problem.Tests.OrderBy(t => t.OrderIndex))
            {
                var testResult = await RunTestCaseAsync(submission, test, workDir);
                results.Add(testResult);
            }

            submission.ResultsJson = JsonSerializer.Serialize(results, JsonOptions);
            submission.Status = results.All(r => r.Status == SubmissionStatus.Accepted) ? SubmissionStatus.Accepted : SubmissionStatus.WrongAnswer;
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

    private string ToCamelCase(string snakeCase) => Regex.Replace(snakeCase, "_([a-z])", m => m.Groups[1].Value.ToUpper());

    private async Task<TestCaseResult> RunTestCaseAsync(Submission submission, ProblemTest test, string workDir)
    {
        var result = new TestCaseResult { TestId = test.Id, Input = test.InputJson, Expected = test.ExpectedOutputJson.Trim() };
        var lang = submission.Language.ToLower();
        var snakeMethod = submission.Problem.MethodName ?? "solve";
        var camelMethod = ToCamelCase(snakeMethod);
        
        // 1. Prepare files
        await File.WriteAllTextAsync(Path.Combine(workDir, "input.json"), test.InputJson, Utf8NoBom);

        string runCmd = "";
        string compileCmd = "";

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
    res = func(**data) if isinstance(data, dict) else func(data)
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
            await File.WriteAllTextAsync(Path.Combine(workDir, "solution.cpp"), submission.SourceCode, Utf8NoBom);
            // Minimal C++ driver for Two Sum
            var driver = $@"
#include <iostream>
#include <vector>
#include <string>
#include ""solution.cpp""
int main() {{
    Solution sol;
    std::vector<int> nums = {{2, 7, 11, 15}}; // Simplified for prototype
    int target = 9;
    std::vector<int> res = sol.{camelMethod}(nums, target);
    std::cout << ""["";
    for(size_t i=0; i<res.size(); ++i) std::cout << res[i] << (i==res.size()-1 ? """" : "","");
    std::cout << ""]"" << std::endl;
    return 0;
}}";
            await File.WriteAllTextAsync(Path.Combine(workDir, "driver.cpp"), driver, Utf8NoBom);
            compileCmd = "g++ -O3 /app/driver.cpp -o /app/out";
            runCmd = "/app/out";
        }
        else if (lang == "java")
        {
            await File.WriteAllTextAsync(Path.Combine(workDir, "Solution.java"), submission.SourceCode, Utf8NoBom);
            var driver = $@"
import java.util.*;
public class Driver {{
    public static void main(String[] args) throws Exception {{
        Solution sol = new Solution();
        int[] res = sol.{camelMethod}(new int[]{{2, 7, 11, 15}}, 9); // Simplified for prototype
        System.out.println(Arrays.toString(res).replace("" "", """"));
    }}
}}";
            await File.WriteAllTextAsync(Path.Combine(workDir, "Driver.java"), driver, Utf8NoBom);
            compileCmd = "javac /app/Solution.java /app/Driver.java";
            runCmd = "java -cp /app Driver";
        }

        var dockerWorkDir = workDir.Replace("\\", "/");
        var dockerCmd = string.IsNullOrEmpty(compileCmd) ? runCmd : $"{compileCmd} && {runCmd}";

        var psi = new ProcessStartInfo {
            FileName = "docker",
            Arguments = $"run --rm -v \"{dockerWorkDir}:/app\" {GetDockerImage(lang)} sh -c \"cd /app && {dockerCmd}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try {
            using var p = Process.Start(psi);
            var outTask = p.StandardOutput.ReadToEndAsync();
            var errTask = p.StandardError.ReadToEndAsync();
            if (await Task.WhenAny(Task.Delay(TimeSpan.FromSeconds(5)), p.WaitForExitAsync()) == Task.Delay(TimeSpan.FromSeconds(5))) {
                p.Kill(true);
                result.Status = SubmissionStatus.TimeLimitExceeded;
                result.Error = "Time Limit Exceeded";
                return result;
            }
            result.Output = (await outTask).Trim();
            result.Error = await errTask;
            if (p.ExitCode != 0) {
                result.Status = SubmissionStatus.RuntimeError;
            } else {
                result.Status = IsMatch(result.Output, result.Expected) ? SubmissionStatus.Accepted : SubmissionStatus.WrongAnswer;
            }
        } catch (Exception ex) {
            result.Error = ex.Message;
            result.Status = SubmissionStatus.SystemError;
        }
        return result;
    }

    private bool IsMatch(string actual, string expected) {
        // Simple JSON-like comparison (ignore whitespace)
        return actual.Replace(" ", "") == expected.Replace(" ", "");
    }

    private string GetDockerImage(string language) => language.ToLower() switch {
        "python" => "python:3.10-slim",
        "java" => "openjdk:21-slim",
        "cpp" => "gcc:12",
        _ => "alpine"
    };
}
