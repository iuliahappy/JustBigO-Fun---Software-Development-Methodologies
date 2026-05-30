using Ganss.Xss;

namespace JustBigO_Fun_.Services;

/// <summary>
/// Wraps <see cref="Ganss.Xss.HtmlSanitizer"/> with an allow-list tailored to problem statements.
/// Permits common formatting tags plus the <c>jbo-example-box</c> styling used by seeded problems,
/// while removing scripts, inline event handlers, iframes, etc.
/// </summary>
public class HtmlSanitizerService : IHtmlSanitizer
{
    private readonly HtmlSanitizer _sanitizer;

    public HtmlSanitizerService()
    {
        _sanitizer = new HtmlSanitizer();

        // Start from a conservative formatting-only allow-list.
        _sanitizer.AllowedTags.Clear();
        foreach (var tag in new[]
        {
            "p", "br", "strong", "b", "em", "i", "u", "s", "code", "pre",
            "ul", "ol", "li", "blockquote", "h1", "h2", "h3", "h4", "h5", "h6",
            "table", "thead", "tbody", "tr", "th", "td", "div", "span", "a", "hr", "sup", "sub"
        })
        {
            _sanitizer.AllowedTags.Add(tag);
        }

        _sanitizer.AllowedAttributes.Clear();
        _sanitizer.AllowedAttributes.Add("class");
        _sanitizer.AllowedAttributes.Add("href");
        _sanitizer.AllowedAttributes.Add("title");
        _sanitizer.AllowedAttributes.Add("colspan");
        _sanitizer.AllowedAttributes.Add("rowspan");

        // Only allow safe link schemes; no javascript: URIs.
        _sanitizer.AllowedSchemes.Clear();
        _sanitizer.AllowedSchemes.Add("http");
        _sanitizer.AllowedSchemes.Add("https");
        _sanitizer.AllowedSchemes.Add("mailto");
    }

    public string Sanitize(string? html) =>
        string.IsNullOrWhiteSpace(html) ? string.Empty : _sanitizer.Sanitize(html);
}
