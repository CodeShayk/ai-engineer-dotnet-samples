using System.Reflection;

namespace Northwind.Shared.Knowledge;

/// <summary>A Northwind policy document with the metadata from its front matter (Chapter 9.5).</summary>
public sealed record PolicyDocument(
    string Id,
    string Title,
    string Category,
    DateOnly EffectiveDate,
    string Status,
    string Region,
    string Audience,
    string Content);

/// <summary>
/// Loads the policy library embedded in this assembly. Each Markdown file starts with a
/// small front matter block of key: value pairs, followed by the document body.
/// </summary>
public static class PolicyLibrary
{
    private static readonly Lazy<IReadOnlyList<PolicyDocument>> Documents = new(Load);

    public static IReadOnlyList<PolicyDocument> LoadAll() => Documents.Value;

    private static IReadOnlyList<PolicyDocument> Load()
    {
        Assembly assembly = typeof(PolicyLibrary).Assembly;

        return assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith("Policies/", StringComparison.Ordinal) && name.EndsWith(".md", StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .Select(name =>
            {
                using Stream stream = assembly.GetManifestResourceStream(name)!;
                using var reader = new StreamReader(stream);
                string id = Path.GetFileNameWithoutExtension(name["Policies/".Length..]);
                return Parse(id, reader.ReadToEnd());
            })
            .ToList();
    }

    public static PolicyDocument Parse(string id, string markdown)
    {
        string text = markdown.Replace("\r\n", "\n");
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string body = text;

        if (text.StartsWith("---\n", StringComparison.Ordinal))
        {
            int end = text.IndexOf("\n---\n", 4, StringComparison.Ordinal);
            if (end > 0)
            {
                foreach (string line in text[4..end].Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    int colon = line.IndexOf(':');
                    if (colon > 0)
                    {
                        metadata[line[..colon].Trim()] = line[(colon + 1)..].Trim();
                    }
                }

                body = text[(end + 5)..];
            }
        }

        string Get(string key, string fallback) => metadata.TryGetValue(key, out string? value) ? value : fallback;

        return new PolicyDocument(
            Id: id,
            Title: Get("title", id),
            Category: Get("category", "general"),
            EffectiveDate: DateOnly.TryParse(Get("effective_date", ""), out DateOnly date) ? date : DateOnly.MinValue,
            Status: Get("status", "current"),
            Region: Get("region", "global"),
            Audience: Get("audience", "customer"),
            Content: body.Trim());
    }
}
