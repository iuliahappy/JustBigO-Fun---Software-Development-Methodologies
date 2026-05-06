using Microsoft.Extensions.Configuration;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace JustBigO_Fun_.Services;

public class GeminiCodeCompleter : ICodeCompleter
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public GeminiCodeCompleter(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task<string> GenerateCompletionAsync(
        string problemTitle,
        string problemDescription,
        string? methodName,
        string currentCode,
        string language,
        string approachSummary,
        string? testFailureFeedback,
        CancellationToken cancellationToken = default)
    {
        var apiKey = _configuration["GeminiApiKey"]?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(apiKey))
            return currentCode;

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={apiKey}";

        var repairSection = string.IsNullOrWhiteSpace(testFailureFeedback)
            ? ""
            : $"""

            Previous attempt failed automated tests. Fix the code while KEEPING the same algorithmic approach and structure.
            Test / compiler feedback (truncated):
            {testFailureFeedback}
            """;

        var prompt = $"""
            You are the Mentor agent. The Analyzer already described the user's intended approach (pass this to your reasoning; do not contradict it unless impossible):
            {approachSummary}

            Problem title: {problemTitle}
            Required entry method name (snake_case or language convention as in template): {methodName ?? "solve"}
            Problem statement (may contain HTML):
            {problemDescription}

            Programming language: {language}

            User's current partial or draft code — you MUST extend and complete this code, preserving their identifiers, control flow style, and overall paradigm. Do not replace with an unrelated optimal algorithm unless the feedback explicitly requires a fix that cannot be done otherwise.
            {repairSection}

            Current code:
            ```
            {currentCode}
            ```

            Output requirements:
            - Return ONE complete solution file for {language} that compiles and satisfies the problem's public tests.
            - Prefer correctness over asymptotic optimality (O(n^2) is acceptable if it matches the user's approach).
            - Output ONLY one markdown fenced code block using an appropriate language tag (python, java, or cpp). No prose outside the fence.
            """;

        var payload = new
        {
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[] { new { text = prompt } }
                }
            },
            generationConfig = new { temperature = 0.35 }
        };

        try
        {
            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync(url, content, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return currentCode;

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(body);
            var text = doc.RootElement.GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            if (string.IsNullOrWhiteSpace(text))
                return currentCode;

            var extracted = ExtractFencedCode(text);
            return string.IsNullOrWhiteSpace(extracted) ? text.Trim() : extracted.Trim();
        }
        catch
        {
            return currentCode;
        }
    }

    private static string ExtractFencedCode(string text)
    {
        var match = Regex.Match(
            text,
            @"```(?:python|java|cpp|c\+\+)?\s*\r?\n([\s\S]*?)```",
            RegexOptions.IgnoreCase);

        return match.Success ? match.Groups[1].Value : string.Empty;
    }
}
