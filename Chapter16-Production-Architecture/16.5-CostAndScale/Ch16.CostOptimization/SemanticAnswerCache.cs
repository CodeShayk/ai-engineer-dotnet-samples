using System.Collections.Concurrent;
using System.Numerics.Tensors;
using Microsoft.Extensions.AI;

namespace Ch16.CostOptimization;

/// <summary>
/// Reuses an answer when a new question is close enough in meaning to one answered recently
/// (Chapter 16.5). Use it only for questions that do not depend on the customer's own data, and
/// include the knowledge version so a policy change invalidates every dependent answer.
/// </summary>
public sealed class SemanticAnswerCache(IEmbeddingGenerator<string, Embedding<float>> embedder, float similarityThreshold = 0.95f)
{
    private readonly ConcurrentQueue<CacheEntry> _entries = new();

    private sealed record CacheEntry(ReadOnlyMemory<float> Vector, string KnowledgeVersion, string Answer, DateTimeOffset Created);

    public async Task<string?> TryGetAsync(string question, string knowledgeVersion, CancellationToken cancellationToken)
    {
        ReadOnlyMemory<float> vector = await embedder.GenerateVectorAsync(question, cancellationToken: cancellationToken);

        return _entries
            .Where(e => e.KnowledgeVersion == knowledgeVersion && DateTimeOffset.UtcNow - e.Created < TimeSpan.FromHours(24))
            .Select(e => (e.Answer, Score: TensorPrimitives.CosineSimilarity(vector.Span, e.Vector.Span)))
            .Where(x => x.Score >= similarityThreshold)
            .OrderByDescending(x => x.Score)
            .Select(x => x.Answer)
            .FirstOrDefault();
    }

    public async Task AddAsync(string question, string knowledgeVersion, string answer, CancellationToken cancellationToken)
    {
        ReadOnlyMemory<float> vector = await embedder.GenerateVectorAsync(question, cancellationToken: cancellationToken);
        _entries.Enqueue(new CacheEntry(vector, knowledgeVersion, answer, DateTimeOffset.UtcNow));
    }

    /// <summary>The closest cached question's similarity, for calibrating the threshold.</summary>
    public async Task<float> BestScoreAsync(string question, CancellationToken cancellationToken)
    {
        ReadOnlyMemory<float> vector = await embedder.GenerateVectorAsync(question, cancellationToken: cancellationToken);
        return _entries.Select(e => TensorPrimitives.CosineSimilarity(vector.Span, e.Vector.Span)).DefaultIfEmpty(0).Max();
    }
}
