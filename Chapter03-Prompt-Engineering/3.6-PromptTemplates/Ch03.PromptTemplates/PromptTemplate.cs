using System.Globalization;
using System.Text.RegularExpressions;

namespace Ch03.PromptTemplates;

/// <summary>A versioned prompt with named placeholders such as {{question}} (Chapter 3.6).</summary>
public sealed partial record PromptTemplate(
    string Name,
    int Version,
    string Description,
    float? Temperature,
    string Body)
{
    [GeneratedRegex(@"\{\{\s*(?<name>[a-zA-Z_][a-zA-Z0-9_]*)\s*\}\}")]
    private static partial Regex PlaceholderPattern();

    /// <summary>The placeholder names this template expects.</summary>
    public IReadOnlySet<string> Variables =>
        PlaceholderPattern().Matches(Body).Select(m => m.Groups["name"].Value).ToHashSet();

    public string Render(IReadOnlyDictionary<string, string> values)
    {
        var missing = Variables.Where(v => !values.ContainsKey(v)).ToList();
        if (missing.Count > 0)
        {
            throw new ArgumentException(
                $"Prompt '{Name}' v{Version} is missing values for: {string.Join(", ", missing)}.");
        }

        return PlaceholderPattern().Replace(Body, m => values[m.Groups["name"].Value]);
    }
}

/// <summary>Returns templates by name: the highest version by default, or a version pinned in configuration.</summary>
public sealed class PromptLibrary
{
    private readonly Dictionary<string, SortedList<int, PromptTemplate>> _prompts;
    private readonly IReadOnlyDictionary<string, int> _pinnedVersions;

    public PromptLibrary(IEnumerable<PromptTemplate> templates, IReadOnlyDictionary<string, int>? pinnedVersions = null)
    {
        _prompts = templates
            .GroupBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => new SortedList<int, PromptTemplate>(g.ToDictionary(t => t.Version)),
                StringComparer.OrdinalIgnoreCase);

        _pinnedVersions = pinnedVersions ?? new Dictionary<string, int>();
    }

    public IEnumerable<PromptTemplate> All => _prompts.Values.SelectMany(v => v.Values);

    public PromptTemplate Get(string name)
    {
        if (!_prompts.TryGetValue(name, out SortedList<int, PromptTemplate>? versions))
        {
            throw new KeyNotFoundException($"No prompt named '{name}' was found.");
        }

        return _pinnedVersions.TryGetValue(name, out int pinned)
            ? versions[pinned]
            : versions.Values[^1];
    }
}

/// <summary>Loads prompt files: a front matter block (name, version, description, temperature) followed by the body.</summary>
public static class PromptFiles
{
    public static IReadOnlyList<PromptTemplate> LoadAll(string directory)
    {
        string path = Path.IsPathRooted(directory) ? directory : Path.Combine(AppContext.BaseDirectory, directory);

        return Directory.EnumerateFiles(path, "*.md")
            .OrderBy(f => f, StringComparer.Ordinal)
            .Select(f => Parse(File.ReadAllText(f), Path.GetFileName(f)))
            .ToList();
    }

    public static PromptTemplate Parse(string content, string source = "prompt")
    {
        string text = content.Replace("\r\n", "\n");
        if (!text.StartsWith("---\n", StringComparison.Ordinal))
        {
            throw new FormatException($"{source} does not start with a front matter block.");
        }

        int end = text.IndexOf("\n---\n", 4, StringComparison.Ordinal);
        if (end < 0)
        {
            throw new FormatException($"{source} has an unterminated front matter block.");
        }

        var metadata = text[4..end]
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split(':', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim(), StringComparer.OrdinalIgnoreCase);

        return new PromptTemplate(
            Name: metadata.TryGetValue("name", out string? name) ? name : throw new FormatException($"{source} has no name."),
            Version: int.Parse(metadata.GetValueOrDefault("version", "1"), CultureInfo.InvariantCulture),
            Description: metadata.GetValueOrDefault("description", ""),
            Temperature: float.TryParse(metadata.GetValueOrDefault("temperature"), NumberStyles.Float, CultureInfo.InvariantCulture, out float t) ? t : null,
            Body: text[(end + 5)..].Trim());
    }
}
