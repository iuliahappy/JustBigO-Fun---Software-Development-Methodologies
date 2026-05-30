namespace JustBigO_Fun_.Services;

/// <summary>
/// Renders Markdown problem statements into HTML that is safe to emit with <c>@Html.Raw(...)</c>.
/// </summary>
public interface IMarkdownRenderer
{
    /// <summary>
    /// Converts Markdown to HTML and then sanitizes the result. Any raw HTML embedded in the
    /// source (e.g. legacy HTML-authored descriptions) is preserved but still sanitized.
    /// </summary>
    string RenderToSafeHtml(string? markdown);
}
