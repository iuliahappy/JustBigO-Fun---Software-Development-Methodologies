using JustBigO_Fun_.Services;
using Microsoft.Extensions.Configuration;
using System.Text;
using System.Text.Json;

public class AgentComplexityAnalyzer : IComplexityAnalyzer
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    // Constructorul și restul metodelor rămân la fel
    public AgentComplexityAnalyzer(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task<(string TimeComplexity, string SpaceComplexity)> AnalyzeCodeAsync(string sourceCode)
    {
        if (string.IsNullOrWhiteSpace(sourceCode)) return ("O(1)", "O(1)");

        // PRELUARE CHEIE DIN APPSETTINGS
        // În appsettings.json trebuie să ai: "GeminiApiKey": "AIza..."
        string apiKey = _configuration["GeminiApiKey"]?.Trim();

        if (string.IsNullOrEmpty(apiKey))
            return ("Eroare", "Cheie API lipsă în appsettings");

        // URL-ul stabil pentru Gemini 2.5 Flash
        string url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={apiKey}";

        // CURĂȚARE COD (Escaping pentru JSON)
        string escapedCode = sourceCode.Replace("\\", "\\\\")
                                       .Replace("\"", "\\\"")
                                       .Replace("\n", "\\n")
                                       .Replace("\r", "");

        // CONSTRUCȚIE PAYLOAD (Formatul strict Gemini 2.5)
        string jsonPayload = $@"{{
            ""contents"": [{{
                ""role"": ""user"",
                ""parts"": [{{
                    ""text"": ""Analyze the following C# code and return ONLY a JSON object with two fields: 'TimeComplexity' and 'SpaceComplexity'. Code: {escapedCode}""
                }}]
            }}],
            ""generationConfig"": {{
                ""temperature"": 0.1,
                ""response_mime_type"": ""application/json""
            }}
        }}";

        try
        {
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                return ("Err:", "400 - Structură incorectă");
            }

            var responseString = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseString);

            // Parsare structură Google: candidates -> content -> parts -> text
            string aiResponseText = doc.RootElement.GetProperty("candidates")[0]
                                              .GetProperty("content")
                                              .GetProperty("parts")[0]
                                              .GetProperty("text")
                                              .GetString();

            // Parsăm textul primit (care este JSON-ul nostru mic)
            var result = JsonSerializer.Deserialize<JsonElement>(aiResponseText);

            return (
                result.GetProperty("TimeComplexity").GetString() ?? "N/A",
                result.GetProperty("SpaceComplexity").GetString() ?? "N/A"
            );
        }
        catch (Exception ex)
        {
            return ("Eroare", "Parsare eșuată");
        }
    }
}