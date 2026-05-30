namespace JustBigO_Fun_.Services;

/// <summary>
/// Sanitizes untrusted HTML (e.g. admin-authored problem descriptions) so that it can
/// be safely rendered with <c>@Html.Raw(...)</c> without exposing the app to XSS.
/// </summary>
public interface IHtmlSanitizer
{
    /// <summary>Strips scripts, event handlers and other dangerous markup, keeping only safe formatting tags.</summary>
    string Sanitize(string? html);
}
