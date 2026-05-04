using Microsoft.Extensions.Configuration;
using System.Text;
using System.Text.Json;

namespace JustBigO_Fun_.Services;

public class GeminiApproachAnalyzer : IApproachAnalyzer
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public GeminiApproachAnalyzer(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task<string> DescribeApproachAsync(string partialCode, string language, CancellationToken cancellationToken = default)
    {
        var apiKey = _configuration["GeminiApiKey"]?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(apiKey))
            return "No API key configured; approach not analyzed.";

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={apiKey}";

        var prompt = $"""
            You are the Analyzer agent for a coding platform.
            The user wrote partial {language} code (it may be incomplete or non-compiling).
            Identify the algorithmic paradigm or pattern they are heading toward (e.g. brute force, two pointers, hash map, sorting, recursion, sliding window).
            Summarize the current logic in 1-2 short English sentences.

            Partial code:
            {partialCode}

            Respond with ONLY a JSON object with keys "paradigm" and "logicSummary" (both strings).
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
            generationConfig = new
            {
                temperature = 0.2,
                response_mime_type = "application/json"
            }
        };

        try
        {
            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync(url, content, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return "Approach could not be analyzed (API error).";

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(body);
            var text = doc.RootElement.GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            if (string.IsNullOrWhiteSpace(text))
                return "Approach could not be analyzed (empty response).";

            using var inner = JsonDocument.Parse(text);
            var root = inner.RootElement;
            var paradigm = root.TryGetProperty("paradigm", out var p) ? p.GetString() ?? "" : "";
            var logic = root.TryGetProperty("logicSummary", out var l) ? l.GetString() ?? "" : "";
            return $"Paradigm: {paradigm.Trim()}. Current logic: {logic.Trim()}";
        }
        catch
        {
            return "Approach could not be analyzed (parse error).";
        }
    }
}
