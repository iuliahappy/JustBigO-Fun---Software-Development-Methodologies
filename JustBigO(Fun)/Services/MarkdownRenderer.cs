using Markdig;

namespace JustBigO_Fun_.Services;

/// <summary>
/// Renders Markdown with Markdig (advanced extensions: tables, lists, etc.) and runs the
/// resulting HTML through <see cref="IHtmlSanitizer"/> so it is safe to render unescaped.
/// </summary>
public class MarkdownRenderer : IMarkdownRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    private readonly IHtmlSanitizer _sanitizer;

    public MarkdownRenderer(IHtmlSanitizer sanitizer)
    {
        _sanitizer = sanitizer;
    }

    public string RenderToSafeHtml(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return string.Empty;

        var html = Markdown.ToHtml(markdown, Pipeline);
        return _sanitizer.Sanitize(html);
    }
}
