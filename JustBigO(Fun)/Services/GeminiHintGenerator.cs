using Microsoft.Extensions.Configuration;
using System.Text;
using System.Text.Json;

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
            Return exactly one concise hint in English, max 2 short paragraphs.
            Do NOT provide full solution code.
            Keep the hint actionable and contextual.
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

            return hintText.Trim();
        }
        catch
        {
            return "An error occurred while generating the hint. Please try again.";
        }
    }
}
