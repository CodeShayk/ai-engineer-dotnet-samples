using System.Text;
using Microsoft.ML.Tokenizers;

namespace Northwind.Shared.Knowledge;

public sealed record DocumentChunk(string ChunkId, string DocumentId, string Title, string Section, int Index, string Text);

/// <summary>
/// A structure-aware chunker for Markdown (Chapter 9.3). Splits at headings, keeps each
/// section whole when it fits the token budget, splits larger sections at paragraph
/// boundaries with overlap, and prefixes every chunk with its title and section path.
/// </summary>
public sealed class MarkdownSectionChunker(Tokenizer tokenizer, int maxTokens = 350, int overlapParagraphs = 1)
{
    /// <summary>Creates a chunker using the o200k_base tokenizer.</summary>
    public static MarkdownSectionChunker CreateDefault(int maxTokens = 350, int overlapParagraphs = 1) =>
        new(TiktokenTokenizer.CreateForEncoding("o200k_base"), maxTokens, overlapParagraphs);

    public IEnumerable<DocumentChunk> Chunk(string documentId, string title, string markdown)
    {
        int index = 0;

        foreach ((string section, string body) in SplitIntoSections(markdown.Replace("\r\n", "\n")))
        {
            string header = string.IsNullOrEmpty(section) ? title : $"{title} > {section}";

            foreach (string text in SplitToBudget(body, header))
            {
                yield return new DocumentChunk($"{documentId}#{index}", documentId, title, section, index, text);
                index++;
            }
        }
    }

    private static IEnumerable<(string Section, string Body)> SplitIntoSections(string markdown)
    {
        string current = "";
        var body = new StringBuilder();

        foreach (string line in markdown.Split('\n'))
        {
            if (line.StartsWith("## ") || line.StartsWith("### "))
            {
                if (body.ToString().Trim().Length > 0)
                {
                    yield return (current, body.ToString().Trim());
                }

                current = line.TrimStart('#', ' ').Trim();
                body.Clear();
            }
            else if (!line.StartsWith("# "))   // The document title is carried separately.
            {
                body.AppendLine(line);
            }
        }

        if (body.ToString().Trim().Length > 0)
        {
            yield return (current, body.ToString().Trim());
        }
    }

    private IEnumerable<string> SplitToBudget(string body, string header)
    {
        // AppendLine writes Environment.NewLine, so normalize line endings before splitting.
        string[] paragraphs = body.Replace("\r\n", "\n")
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var current = new List<string>();

        foreach (string paragraph in paragraphs)
        {
            if (current.Count > 0 && CountTokens(header, [.. current, paragraph]) > maxTokens)
            {
                yield return Compose(header, current);

                // Carry overlap forward, but only if the next chunk still fits the budget with it.
                current = current.TakeLast(overlapParagraphs).ToList();
                if (CountTokens(header, [.. current, paragraph]) > maxTokens)
                {
                    current.Clear();
                }
            }

            current.Add(paragraph);
        }

        if (current.Count > 0)
        {
            yield return Compose(header, current);
        }
    }

    private int CountTokens(string header, IEnumerable<string> paragraphs) =>
        tokenizer.CountTokens(Compose(header, paragraphs));

    private static string Compose(string header, IEnumerable<string> paragraphs) =>
        $"{header}\n\n{string.Join("\n\n", paragraphs)}";
}
