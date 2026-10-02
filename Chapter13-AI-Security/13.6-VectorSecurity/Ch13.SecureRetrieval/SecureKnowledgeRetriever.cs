using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;

namespace Ch13.SecureRetrieval;

/// <summary>Who is asking, as established by authentication, never by the model or the query.</summary>
public sealed record CallerIdentity(string TenantId, bool IsStaff);

/// <summary>
/// The only way to search the knowledge base (Chapter 13.6). Every search is constrained to the
/// caller's tenant, to audiences the caller may see and to current documents, and there is no
/// method that skips those filters.
/// </summary>
public sealed class SecureKnowledgeRetriever(
    VectorStoreCollection<string, KnowledgeChunkRecord> collection,
    IEmbeddingGenerator<string, Embedding<float>> embedder)
{
    public async Task<IReadOnlyList<KnowledgeChunkRecord>> SearchAsync(
        CallerIdentity caller, string query, int top, CancellationToken cancellationToken)
    {
        ReadOnlyMemory<float> vector = await embedder.GenerateVectorAsync(query, cancellationToken: cancellationToken);

        string tenantId = caller.TenantId;
        string audience = caller.IsStaff ? "internal" : "customer";

        var options = new VectorSearchOptions<KnowledgeChunkRecord>
        {
            // Mandatory: the caller's tenant, an audience they may see, current documents only.
            Filter = c => c.TenantId == tenantId
                       && (c.Audience == "customer" || c.Audience == audience)
                       && c.Status == "current"
        };

        var results = new List<KnowledgeChunkRecord>();
        await foreach (VectorSearchResult<KnowledgeChunkRecord> r in collection.SearchAsync(vector, top, options, cancellationToken))
        {
            results.Add(r.Record);
        }

        return results;
    }
}
