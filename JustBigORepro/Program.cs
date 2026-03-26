using System;
using System.IO;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

class Program
{
    static async Task Main()
    {
        var Utf8NoBom = new UTF8Encoding(false);
        var workDir = Path.Combine(Path.GetTempPath(), "justbigo_repro");
        if (Directory.Exists(workDir)) Directory.Delete(workDir, true);
        Directory.CreateDirectory(workDir);

        string sourceCode = @"
def two_sum(nums, target):
    n = len(nums)
    for i in range(n - 1):
        for j in range(i + 1, n):
            if nums[i] + nums[j] == target:
                return [i, j]
    return []
";
        string inputJson = "{\"nums\":[2,7,11,15],\"target\":9}";
        string methodName = "two_sum";

        await File.WriteAllTextAsync(Path.Combine(workDir, "solution.py"), sourceCode, Utf8NoBom);
        await File.WriteAllTextAsync(Path.Combine(workDir, "input.json"), inputJson, Utf8NoBom);

        string driver = $@"
import json
import sys
import os
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
        await File.WriteAllTextAsync(Path.Combine(workDir, "driver.py"), driver, Utf8NoBom);

        string dockerWorkDir = workDir.Replace("\\", "/");
        Console.WriteLine($"Running Docker with workDir: {dockerWorkDir}");

        var psi = new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = $"run --rm -v \"{dockerWorkDir}:/app\" python:3.10-slim sh -c \"cd /app && python /app/driver.py\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var p = Process.Start(psi);
        var stdout = await p.StandardOutput.ReadToEndAsync();
        var stderr = await p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync();

        Console.WriteLine("--- STDOUT ---");
        Console.WriteLine(stdout);
        Console.WriteLine("--- STDERR ---");
        Console.WriteLine(stderr);
        Console.WriteLine($"Exit Code: {p.ExitCode}");
    }
}
