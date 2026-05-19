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

        var sw = Stopwatch.StartNew();
        try
        {
            var (status, resultsJson, errorMessage, userTime, peakMemory) = await EvaluateInWorkDirAsync(submission, workDir);
            submission.ResultsJson = resultsJson;
            submission.Status = status;
            submission.ErrorMessage = errorMessage;
            submission.UserTimeMs = userTime;
            submission.PeakMemoryKb = peakMemory;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing submission {SubmissionId}", submission.Id);
            submission.Status = SubmissionStatus.SystemError;
            submission.ErrorMessage = ex.Message;
        }
        finally
        {
            sw.Stop();
            submission.ExecutionTimeMs = sw.Elapsed.TotalMilliseconds;
            try { Directory.Delete(workDir, true); } catch { }
            await db.SaveChangesAsync();
        }
    }

    public async Task<(SubmissionStatus Status, string ResultsJson, string? ErrorMessage)> EvaluateCodeAsync(
        int problemId,
        string sourceCode,
        string language,
        CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var problem = await db.Problems
            .Include(p => p.Tests)
            .FirstOrDefaultAsync(p => p.Id == problemId, cancellationToken);

        if (problem == null)
            return (SubmissionStatus.SystemError, "[]", "Problem not found.");

        var submission = new Submission
        {
            ProblemId = problemId,
            Problem = problem,
            SourceCode = sourceCode,
            Language = language
        };

        var workDir = Path.Combine(Path.GetTempPath(), "justbigo-eval", Guid.NewGuid().ToString("N"));
        if (Directory.Exists(workDir)) Directory.Delete(workDir, true);
        Directory.CreateDirectory(workDir);

        try
        {
            var res = await EvaluateInWorkDirAsync(submission, workDir);
            return (res.Status, res.ResultsJson, res.ErrorMessage);
        }
        finally
        {
            try { Directory.Delete(workDir, true); } catch { }
        }
    }

    private async Task<(SubmissionStatus Status, string ResultsJson, string? ErrorMessage, double UserTime, double PeakMemory)> EvaluateInWorkDirAsync(
        Submission submission,
        string workDir)
    {
        var compileResult = await PrepareAndCompileAsync(submission, workDir);
        if (!compileResult.Success)
        {
            return (SubmissionStatus.CompilationError, "[]", compileResult.Error, 0, 0);
        }

        var results = new List<TestCaseResult>();
        foreach (var test in submission.Problem.Tests.OrderBy(t => t.OrderIndex))
        {
            var testResult = await RunTestCaseAsync(submission, test, workDir);
            results.Add(testResult);
        }

        var resultsJson = JsonSerializer.Serialize(results, JsonOptions);
        var status = AggregateStatus(results);

        string? errorMessage = null;
        if (status != SubmissionStatus.Accepted && status != SubmissionStatus.WrongAnswer)
        {
            var firstError = results.FirstOrDefault(r => r.Status == status);
            errorMessage = firstError?.Error;
        }

        double maxMemory = results.Any() ? results.Max(r => r.PeakMemoryKb) : 0;
        double maxTime = results.Any() ? results.Max(r => r.UserTimeMs) : 0;

        return (status, resultsJson, errorMessage, maxTime, maxMemory);
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

        if (lang == "python")
        {
            await File.WriteAllTextAsync(Path.Combine(workDir, "solution.py"), submission.SourceCode, Utf8NoBom);
            var checkArgs = BuildDockerArguments(workDir, "python -m py_compile solution.py", "python");
            var checkResult = await RunDockerCommandAsync(checkArgs, 10);
            if (checkResult.ExitCode != 0)
            {
                var combinedError = string.Join("\n", new[] { checkResult.Error, checkResult.Output }.Where(s => !string.IsNullOrWhiteSpace(s)));
                return (false, combinedError);
            }
            return (true, null);
        }
        else if (lang == "cpp")
        {
            await File.WriteAllTextAsync(Path.Combine(workDir, "solution.cpp"), submission.SourceCode, Utf8NoBom);
            var compileCmd = "g++ -O3 solution.cpp -o out";
            var compileArgs = BuildDockerArguments(workDir, compileCmd, "cpp");
            var result = await RunDockerCommandAsync(compileArgs, 30);
            
            if (result.ExitCode != 0)
            {
                var combinedError = string.Join("\n", new[] { result.Error, result.Output }.Where(s => !string.IsNullOrWhiteSpace(s)));
                if (string.IsNullOrWhiteSpace(combinedError))
                {
                    if (result.ExitCode == 137) combinedError = "Compilation failed: Memory limit exceeded (512MB).";
                    else combinedError = $"G++ exited with code {result.ExitCode}";
                }
                return (false, combinedError);
            }
            return (true, null);
        }
        else if (lang == "java")
        {
            var className = GetJavaClassName(submission.SourceCode);
            await File.WriteAllTextAsync(Path.Combine(workDir, $"{className}.java"), submission.SourceCode, Utf8NoBom);
            var compileCmd = $"javac {className}.java";
            var compileArgs = BuildDockerArguments(workDir, compileCmd, "java");
            var result = await RunDockerCommandAsync(compileArgs, 30);
            
            if (result.ExitCode != 0)
            {
                var combinedError = string.Join("\n", new[] { result.Error, result.Output }.Where(s => !string.IsNullOrWhiteSpace(s)));
                if (string.IsNullOrWhiteSpace(combinedError))
                {
                    if (result.ExitCode == 137) combinedError = "Compilation failed: Memory limit exceeded (512MB).";
                    else combinedError = $"Javac exited with code {result.ExitCode}";
                }
                return (false, combinedError);
            }
            return (true, null);
        }

        return (false, $"Unsupported language: {lang}");
    }

    private string GetJavaClassName(string sourceCode)
    {
        var match = Regex.Match(sourceCode, @"class\s+([A-Za-z0-9_]+)");
        return match.Success ? match.Groups[1].Value : "Main";
    }

    private async Task<TestCaseResult> RunTestCaseAsync(Submission submission, ProblemTest test, string workDir)
    {
        var result = new TestCaseResult { TestId = test.Id, Input = test.InputJson, Expected = test.ExpectedOutputJson.Trim() };
        var lang = submission.Language.ToLower();
        
        await File.WriteAllTextAsync(Path.Combine(workDir, "input.txt"), test.InputJson, Utf8NoBom);

        string runCmd = lang switch
        {
            "python" => "python /app/solution.py < /app/input.txt",
            "cpp" => "/app/out < /app/input.txt",
            "java" => $"java -cp /app {GetJavaClassName(submission.SourceCode)} < /app/input.txt",
            _ => throw new Exception("Unsupported language")
        };

        // Use 'time -v' to capture resource usage.
        // Wrap 'timeout' to ensure it doesn't run forever.
        var timedCmd = $"/usr/bin/time -v timeout 5s sh -c \"{runCmd}\"";

        var dockerArgs = BuildDockerArguments(workDir, timedCmd, lang);
        var dockerResult = await RunDockerCommandAsync(dockerArgs, 10); 

        MapDockerResultToStatus(result, dockerResult.ExitCode, dockerResult.Error, dockerResult.Output);

        return result;
    }

    internal void MapDockerResultToStatus(TestCaseResult result, int exitCode, string error, string output)
    {
        // Parse metrics from stderr (where time -v outputs)
        var userTimeMatch = Regex.Match(error, @"User time \(seconds\):\s+([0-9.]+)");
        var memoryMatch = Regex.Match(error, @"Maximum resident set size \(kbytes\):\s+(\d+)");

        if (userTimeMatch.Success) result.UserTimeMs = double.Parse(userTimeMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) * 1000;     
        if (memoryMatch.Success) result.PeakMemoryKb = double.Parse(memoryMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        if (exitCode == 124) // timeout command exit code
        {
            result.Status = SubmissionStatus.TimeLimitExceeded;
            result.Error = "Time Limit Exceeded (5s)";
            result.Output = ""; 
        }
        else if (exitCode == 137 || error.Contains("Memory limit exceeded")) 
        {
            result.Status = SubmissionStatus.MemoryLimitExceeded;
            result.Error = "Memory Limit Exceeded (512MB)";
            result.Output = ""; 
        }
        else if (exitCode != 0)
        {
            result.Status = SubmissionStatus.RuntimeError;
            // Clean up error message by removing time -v noise
            result.Error = Regex.Replace(error, @"\tCommand being timed:.*", "", RegexOptions.Singleline).Trim();
            result.Output = ""; 
        }
        else
        {
            result.Output = output.Trim();
            result.Status = IsMatch(result.Output, result.Expected) ? SubmissionStatus.Accepted : SubmissionStatus.WrongAnswer;
        }
    }

    internal string BuildDockerArguments(string workDir, string cmd, string lang)
    {
        var dockerWorkDir = workDir.Replace("\\", "/");
        // Escape quotes for the inner shell command
        var escapedCmd = cmd.Replace("\"", "\\\"");
        return $"run --rm --network none -m 512m --cpus=\"1.0\" -v \"{dockerWorkDir}:/app\" {GetDockerImage(lang)} sh -c \"cd /app && {escapedCmd}\"";
    }

    private async Task<(int ExitCode, string Output, string Error)> RunDockerCommandAsync(string arguments, int timeoutSeconds)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = arguments,
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
            _logger.LogWarning("Docker command timed out: {Args}", arguments);
            return (124, "", "Timed out waiting for Docker client");
        }

        var stdout = await outTask;
        var stderr = await errTask;

        _logger.LogInformation("Docker ExitCode: {ExitCode}\nSTDOUT: {Stdout}\nSTDERR: {Stderr}", p.ExitCode, stdout, stderr);

        return (p.ExitCode, stdout, stderr);
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
            var dockerArgs = BuildDockerArguments(workDir, cmd, language);
            var result = await RunDockerCommandAsync(dockerArgs, 10);

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
    public double UserTimeMs { get; set; }
    public double PeakMemoryKb { get; set; }
}

public class ProblemSignature
{
    public List<Parameter> Parameters { get; set; } = new();
    public string ReturnType { get; set; } = "void";
}

public class Parameter
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
}
