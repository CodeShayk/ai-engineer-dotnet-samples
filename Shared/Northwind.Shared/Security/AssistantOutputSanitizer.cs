using System.Text.RegularExpressions;

namespace Northwind.Shared.Security;

/// <summary>
/// Makes model output safe to render as Markdown (Chapter 13.4): removes images, which a browser
/// would fetch without a click, removes raw HTML, and keeps links only to allowlisted hosts over HTTPS.
/// </summary>
public sealed partial class AssistantOutputSanitizer(IReadOnlySet<string> allowedLinkHosts)
{
    [GeneratedRegex(@"!\[[^\]]*\]\([^)]*\)")]
    private static partial Regex MarkdownImage();

    [GeneratedRegex(@"\[(?<text>[^\]]*)\]\((?<url>[^)\s]+)[^)]*\)")]
    private static partial Regex MarkdownLink();

    [GeneratedRegex(@"<(script|style)\b[^>]*>[\s\S]*?</\1\s*>", RegexOptions.IgnoreCase)]
    private static partial Regex ScriptOrStyleBlock();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex HtmlTag();

    public string Sanitize(string markdown)
    {
        string result = MarkdownImage().Replace(markdown, "");    // No images from model output.
        result = ScriptOrStyleBlock().Replace(result, "");         // No scripts or styles, including their contents.
        result = HtmlTag().Replace(result, "");                    // No other raw HTML.

        // Keep link text; keep the link only if it points at an allowed host over HTTPS.
        return MarkdownLink().Replace(result, match =>
        {
            string text = match.Groups["text"].Value;
            return Uri.TryCreate(match.Groups["url"].Value, UriKind.Absolute, out Uri? uri)
                   && uri.Scheme == Uri.UriSchemeHttps
                   && allowedLinkHosts.Contains(uri.Host)
                ? match.Value
                : text;
        });
    }
}
