using System.Diagnostics;
using Northwind.Shared.Knowledge;

namespace Ch15.ObservableAssistant.Telemetry;

/// <summary>
/// Retrieval with a span of its own (Chapter 15.3). It records what was found, as chunk ids and
/// scores, and never the query text or chunk content.
/// </summary>
public sealed class TracedPolicyRetriever(PolicyRetriever inner)
{
    public static readonly ActivitySource Source = new("Northwind.Retrieval");

    public async Task<IReadOnlyList<RetrievedChunk>> SearchAsync(
        string query, CustomerContext customer, int top, CancellationToken cancellationToken)
    {
        using Activity? activity = Source.StartActivity("retrieve policies");
        activity?.SetTag("northwind.retrieval.top_k", top);
        activity?.SetTag("northwind.retrieval.region", customer.Region);

        IReadOnlyList<RetrievedChunk> results = await inner.SearchAsync(query, customer, top, cancellationToken);

        activity?.SetTag("northwind.retrieval.result_count", results.Count);
        activity?.SetTag("northwind.retrieval.chunk_ids", string.Join(",", results.Select(r => r.ChunkId)));
        activity?.SetTag("northwind.retrieval.top_score", results.Count > 0 ? results[0].SimilarityScore : null);

        return results;
    }
}
