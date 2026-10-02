using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Northwind.Shared.Security;

/// <summary>
/// Makes model output safe to show in a browser (Chapter 13.4). It works on the parsed Markdown rather than
/// on the text, because Markdown has several syntaxes for the same element: inline and reference-style
/// images both become an image node, so one check removes them all. Images are removed, because a browser
/// fetches them without a click; links are kept only to allowlisted hosts over HTTPS; raw HTML is shown as
/// text. The result is HTML, so the browser never has to interpret Markdown written by the model.
/// </summary>
public sealed class AssistantOutputSanitizer(IReadOnlySet<string> allowedLinkHosts)
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().DisableHtml().Build();

    public string ToSafeHtml(string markdown)
    {
        MarkdownDocument document = Markdown.Parse(markdown, Pipeline);

        foreach (LinkInline link in document.Descendants<LinkInline>().ToList())
        {
            if (link.IsImage)
            {
                link.Remove();                  // Inline and reference-style images alike.
            }
            else if (!IsAllowed(link.Url))
            {
                Unwrap(link);                   // Keep the link text, drop the link.
            }
        }

        foreach (AutolinkInline autolink in document.Descendants<AutolinkInline>().ToList())
        {
            if (!IsAllowed(autolink.Url))
            {
                autolink.ReplaceBy(new LiteralInline(autolink.Url));
            }
        }

        return Markdown.ToHtml(document, Pipeline);
    }

    private bool IsAllowed(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && allowedLinkHosts.Contains(uri.Host);

    private static void Unwrap(LinkInline link)
    {
        while (link.FirstChild is { } child)
        {
            child.Remove();
            link.InsertBefore(child);
        }

        link.Remove();
    }
}
