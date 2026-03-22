using System.ComponentModel.DataAnnotations;

namespace JustBigO_Fun_.Models;

public class Problem
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Slug { get; set; } = string.Empty;

    /// <summary>HTML content for the problem statement.</summary>
    [Required]
    public string Description { get; set; } = string.Empty;

    /// <summary>Easy, Medium, Hard</summary>
    [Required]
    [MaxLength(20)]
    public string Difficulty { get; set; } = "Easy";

    /// <summary>Comma-separated tags: "Array,Hash Map"</summary>
    [MaxLength(500)]
    public string Tags { get; set; } = string.Empty;

    /// <summary>JSON: { "python": "...", "java": "...", "cpp": "..." }</summary>
    public string CodeTemplatesJson { get; set; } = "{}";

    public int OrderIndex { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ProblemTest> Tests { get; set; } = new List<ProblemTest>();

    /// <summary>Returns tags as array for display.</summary>
    public string[] GetTags() =>
        string.IsNullOrWhiteSpace(Tags)
            ? Array.Empty<string>()
            : Tags.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    /// <summary>Returns difficulty CSS class suffix (easy, medium, hard).</summary>
    public string GetDifficultyClass() =>
        Difficulty?.ToLowerInvariant() switch
        {
            "medium" => "medium",
            "hard" => "hard",
            _ => "easy"
        };

    /// <summary>Parses CodeTemplatesJson and returns language -> code template.</summary>
    public Dictionary<string, string> GetCodeTemplates()
    {
        if (string.IsNullOrWhiteSpace(CodeTemplatesJson)) return new Dictionary<string, string>();
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(CodeTemplatesJson)
                ?? new Dictionary<string, string>();
        }
        catch
        {
            return new Dictionary<string, string>();
        }
    }
}
