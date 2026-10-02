using System.Text.RegularExpressions;

namespace Ch08.HybridSearch;

/// <summary>
/// A small in-memory keyword index ranked with BM25, the classic relevance function used by
/// search engines. Good enough to demonstrate hybrid search; in production, use a store with
/// built-in full-text search (Section 8.3).
/// </summary>
public sealed partial class Bm25Index
{
    private const double K1 = 1.2;   // How quickly repeated terms stop adding to the score
    private const double B = 0.75;   // How strongly long documents are penalized

    private static readonly HashSet<string> StopWords =
        ["a", "an", "and", "are", "for", "i", "in", "is", "it", "my", "of", "on", "or", "the", "to", "with", "does", "do", "can"];

    private readonly Dictionary<string, List<string>> _documents = [];
    private readonly Dictionary<string, int> _documentFrequency = [];
    private double _averageLength;

    // Keeps hyphenated codes such as "td-35" together as a single token.
    [GeneratedRegex(@"[\p{L}\p{N}]+(?:-[\p{L}\p{N}]+)*")]
    private static partial Regex TokenPattern();

    public void Add(string id, string text)
    {
        List<string> tokens = Tokenize(text);
        _documents[id] = tokens;

        foreach (string term in tokens.Distinct())
        {
            _documentFrequency[term] = _documentFrequency.GetValueOrDefault(term) + 1;
        }

        _averageLength = _documents.Values.Average(d => d.Count);
    }

    public IReadOnlyList<(string Id, double Score)> Search(string query, int top)
    {
        List<string> queryTerms = Tokenize(query).Distinct().ToList();
        int documentCount = _documents.Count;

        return _documents
            .Select(document => (Id: document.Key, Score: queryTerms.Sum(term => TermScore(term, document.Value, documentCount))))
            .Where(result => result.Score > 0)
            .OrderByDescending(result => result.Score)
            .Take(top)
            .ToList();
    }

    private double TermScore(string term, List<string> document, int documentCount)
    {
        int frequency = document.Count(t => t == term);
        if (frequency == 0)
        {
            return 0;
        }

        // Rare terms (like a product code) weigh far more than common ones.
        int containing = _documentFrequency.GetValueOrDefault(term);
        double idf = Math.Log(1 + (documentCount - containing + 0.5) / (containing + 0.5));

        return idf * frequency * (K1 + 1) / (frequency + K1 * (1 - B + B * document.Count / _averageLength));
    }

    private static List<string> Tokenize(string text) =>
        TokenPattern().Matches(text.ToLowerInvariant())
            .Select(m => m.Value)
            .Where(token => !StopWords.Contains(token))
            .ToList();
}
