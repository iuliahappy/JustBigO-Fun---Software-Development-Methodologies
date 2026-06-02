using Microsoft.Extensions.Configuration;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace JustBigO_Fun_.Services;

public class GeminiHintGenerator : IHintGenerator
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public GeminiHintGenerator(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task<string> GenerateHintAsync(string problemTitle, string problemDescription, string sourceCode, string language)
    {
        string apiKey = _configuration["GeminiApiKey"]?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return "I cannot generate a hint right now. GeminiApiKey is missing.";
        }

        string url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={apiKey}";
        string safeCode = string.IsNullOrWhiteSpace(sourceCode) ? "(no code provided yet)" : sourceCode;

        var prompt = $"""
            You are a programming mentor for an algorithms platform.
            Return exactly ONE subtle hint in English, maximum 1-2 short sentences.
            STRICT RULES:
            - Do NOT provide code, pseudocode, or step-by-step algorithm.
            - Do NOT reveal the full strategy or optimal full solution.
            - Prefer a nudge: mention one data structure, one invariant, or one leading question.
            - If the user is already close, only point to the next tiny step.
            - Keep it spoiler-free and concise.
            Note: The platform uses a Standard IO model (Competitive Programming style). The user must READ from stdin and PRINT to stdout.

            Problem title: {problemTitle}
            Programming language: {language}
            Problem statement (HTML possible):
            {problemDescription}

            User's current code:
            {safeCode}
            """;

        var payload = new
        {
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[]
                    {
                        new { text = prompt }
                    }
                }
            },
            generationConfig = new
            {
                temperature = 0.4
            }
        };

        try
        {
            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                return "I could not generate a hint at the moment. Please try again in a few seconds.";
            }

            var body = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);

            var hintText = doc.RootElement.GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            if (string.IsNullOrWhiteSpace(hintText))
            {
                return "No valid hint was returned. Please try again.";
            }

            return SanitizeHint(hintText);
        }
        catch
        {
            return "An error occurred while generating the hint. Please try again.";
        }
    }

    private static string SanitizeHint(string hintText)
    {
        if (string.IsNullOrWhiteSpace(hintText))
            return "Try focusing on one small invariant that must stay true at each step.";

        // Remove fenced code blocks or inline code to avoid accidental full solutions.
        var sanitized = Regex.Replace(hintText, "```[\\s\\S]*?```", string.Empty);
        sanitized = Regex.Replace(sanitized, "`[^`]*`", string.Empty).Trim();

        // Keep only first 1-2 sentences to force a subtle nudge.
        var sentenceMatches = Regex.Matches(sanitized, @"[^.!?]+[.!?]?");
        if (sentenceMatches.Count > 0)
        {
            var sentenceCount = Math.Min(2, sentenceMatches.Count);
            var parts = new List<string>(sentenceCount);
            for (var i = 0; i < sentenceCount; i++)
            {
                var s = sentenceMatches[i].Value.Trim();
                if (!string.IsNullOrWhiteSpace(s))
                    parts.Add(s);
            }

            sanitized = string.Join(" ", parts).Trim();
        }

        if (string.IsNullOrWhiteSpace(sanitized))
            return "Try focusing on one small invariant that must stay true at each step.";

        // Gentle length cap to reduce chance of spoiler-ish detail.
        if (sanitized.Length > 260)
            sanitized = sanitized[..260].TrimEnd() + "...";

        return sanitized;
    }
}
