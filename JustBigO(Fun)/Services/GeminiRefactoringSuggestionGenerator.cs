using Microsoft.Extensions.Configuration;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace JustBigO_Fun_.Services;

public class GeminiRefactoringSuggestionGenerator : IRefactoringSuggestionGenerator
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public GeminiRefactoringSuggestionGenerator(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task<RefactoringSuggestionResult> GenerateAsync(
        string problemTitle,
        string problemDescription,
        string acceptedSourceCode,
        string language,
        CancellationToken cancellationToken = default)
    {
        var apiKey = _configuration["GeminiApiKey"]?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new RefactoringSuggestionResult(
                string.Empty,
                string.Empty,
                "Gemini API key is not configured; cannot generate refactoring suggestions.");
        }

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={apiKey}";

        // Use delimiter sections instead of strict JSON — user code often contains ", \, newlines that break JSON parsing.
        var prompt = $"""
            You are the Mentor agent. The user already has a solution that passes all functional tests (Docker judge).
            Compare their current approach with the typical OPTIMAL solution for this problem (better asymptotic complexity and/or better data structures when applicable).

            Problem title: {problemTitle}
            Problem statement (HTML allowed):
            {problemDescription}

            Programming language: {language}

            User's current accepted solution:
            ```
            {acceptedSourceCode}
            ```

            Reply in English using EXACTLY these section markers and nothing before the first marker:
            ---CODE---
            (Paste or quote the main fragment of the USER's code worth refactoring — can be multiple lines. Or briefly name the region, e.g. "nested for-loops in two_sum".)
            ---OPTIMAL---
            (Short description of optimal data structure / pattern, e.g. hash map for O(n) lookups.)
            ---STEPS---
            (Numbered refactoring steps, read-only guidance. Do not output a full replacement file.)

            Do not wrap the whole answer in markdown code fences. Each section must start on its own line with ---CODE---, ---OPTIMAL---, and ---STEPS--- (optional spaces inside the dashes, e.g. --- CODE ---).
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
                temperature = 0.25
            }
        };

        try
        {
            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync(url, content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync(cancellationToken);
                return new RefactoringSuggestionResult(
                    string.Empty,
                    string.Empty,
                    $"AI service error ({(int)response.StatusCode}). Try again later.");
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(body);

            if (!doc.RootElement.TryGetProperty("candidates", out var candidates) ||
                candidates.GetArrayLength() == 0)
            {
                return new RefactoringSuggestionResult(
                    string.Empty,
                    string.Empty,
                    "No response from the model (empty candidates). Check API quota or safety filters.");
            }

            var text = candidates[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            if (string.IsNullOrWhiteSpace(text))
            {
                return new RefactoringSuggestionResult(
                    string.Empty,
                    string.Empty,
                    "Empty response from the Mentor agent.");
            }

            var parsed = ParseSectionedResponse(text);
            if (parsed != null &&
                (!string.IsNullOrWhiteSpace(parsed.CodeBlockToModify)
                 || !string.IsNullOrWhiteSpace(parsed.OptimalDataStructure)
                 || !string.IsNullOrWhiteSpace(parsed.RefactoringSteps)))
                return parsed;

            // Fallback: try JSON-shaped reply if model ignored instructions
            var normalized = StripMarkdownJsonFence(text.Trim());
            try
            {
                using var inner = JsonDocument.Parse(normalized);
                var root = inner.RootElement;
                var code = ReadJsonString(root, "codeBlockToModify", "code_block_to_modify");
                var ds = ReadJsonString(root, "optimalDataStructure", "optimal_data_structure");
                var steps = ReadJsonString(root, "refactoringSteps", "refactoring_steps");
                if (!string.IsNullOrWhiteSpace(code) || !string.IsNullOrWhiteSpace(ds) || !string.IsNullOrWhiteSpace(steps))
                    return new RefactoringSuggestionResult(code.Trim(), ds.Trim(), steps.Trim());
            }
            catch
            {
                // ignore
            }

            return new RefactoringSuggestionResult(
                string.Empty,
                string.Empty,
                "Could not parse the mentor response. Raw excerpt:\n" + Truncate(text, 1200));
        }
        catch (Exception ex)
        {
            return new RefactoringSuggestionResult(
                string.Empty,
                string.Empty,
                "Failed to get refactoring suggestions: " + ex.Message);
        }
    }

    private static readonly Regex MarkerCode = new(@"\-\-\-\s*CODE\s*\-\-\-", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex MarkerOptimal = new(@"\-\-\-\s*OPTIMAL\s*\-\-\-", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex MarkerSteps = new(@"\-\-\-\s*STEPS\s*\-\-\-", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static RefactoringSuggestionResult? ParseSectionedResponse(string text)
    {
        (int start, int end, bool ok) Find(Regex rx)
        {
            var m = rx.Match(text);
            return m.Success ? (m.Index, m.Index + m.Length, true) : (0, 0, false);
        }

        var c = Find(MarkerCode);
        var o = Find(MarkerOptimal);
        var s = Find(MarkerSteps);
        if (!c.ok && !o.ok && !s.ok)
            return null;

        var markerStarts = new List<int>(3);
        if (c.ok) markerStarts.Add(c.start);
        if (o.ok) markerStarts.Add(o.start);
        if (s.ok) markerStarts.Add(s.start);
        markerStarts.Sort();

        int NextMarkerStartAfter(int markerStart)
        {
            var next = text.Length;
            foreach (var st in markerStarts)
            {
                if (st > markerStart) next = Math.Min(next, st);
            }

            return next;
        }

        static string Slice(string t, int from, int to) =>
            from >= 0 && to >= from && to <= t.Length ? t[from..to].Trim() : string.Empty;

        var code = c.ok ? Slice(text, c.end, NextMarkerStartAfter(c.start)) : string.Empty;
        var optimal = o.ok ? Slice(text, o.end, NextMarkerStartAfter(o.start)) : string.Empty;
        var steps = s.ok ? Slice(text, s.end, NextMarkerStartAfter(s.start)) : string.Empty;

        return new RefactoringSuggestionResult(code, optimal, steps);
    }

    private static string StripMarkdownJsonFence(string text)
    {
        var m = Regex.Match(text, @"^```(?:json)?\s*\r?\n(.*)\r?\n```\s*$", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value.Trim() : text;
    }

    private static string ReadJsonString(JsonElement root, params string[] propertyNames)
    {
        foreach (var name in propertyNames)
        {
            if (root.TryGetProperty(name, out var el))
            {
                return el.ValueKind == JsonValueKind.String ? el.GetString() ?? "" : el.ToString();
            }
        }

        return string.Empty;
    }

    private static string Truncate(string s, int max)
    {
        if (string.IsNullOrEmpty(s) || s.Length <= max) return s;
        return s[..max] + "…";
    }
}
