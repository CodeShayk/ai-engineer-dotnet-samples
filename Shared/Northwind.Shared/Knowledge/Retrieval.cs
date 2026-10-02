using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using Northwind.Shared.Domain;

namespace Northwind.Shared.Knowledge;

/// <summary>Facts about the customer that shape retrieval, such as which regional policies apply.</summary>
public sealed record CustomerContext(string Region, string? CustomerId = null)
{
    public static CustomerContext For(Customer customer) => new(RegionFor(customer.Country), customer.CustomerId);

    public static string RegionFor(string country) => country switch
    {
        "United Kingdom" => "uk",
        "United States" => "us",
        _ => "global"
    };
}

/// <summary>A retrieved policy passage, as used for prompts, citations and evaluation.</summary>
public sealed record RetrievedChunk(
    string ChunkId,
    string DocumentId,
    string Title,
    string Section,
    string Text,
    string EffectiveDate,
    double SimilarityScore)
{
    public static RetrievedChunk From(PolicyChunkRecord record, double score) =>
        new(record.ChunkId, record.DocumentId, record.Title, record.Section, record.Text, record.EffectiveDate, score);
}

/// <summary>
/// Filtered vector search over the policy library (Chapter 9.5). Hard rules, such as
/// current documents only, customer audience and the customer's region, are applied as a
/// filter inside the search, never afterward.
/// </summary>
public sealed class PolicyRetriever(
    VectorStoreCollection<string, PolicyChunkRecord> collection,
    IEmbeddingGenerator<string, Embedding<float>> embedder)
{
    public async Task<IReadOnlyList<RetrievedChunk>> SearchAsync(
        string query, CustomerContext customer, int top, CancellationToken cancellationToken)
    {
        ReadOnlyMemory<float> vector = await embedder.GenerateVectorAsync(query, cancellationToken: cancellationToken);
        string region = customer.Region;

        var options = new VectorSearchOptions<PolicyChunkRecord>
        {
            Filter = c => c.Status == "current"
                       && c.Audience == "customer"
                       && (c.Region == "global" || c.Region == region)
        };

        var results = new List<RetrievedChunk>();
        await foreach (VectorSearchResult<PolicyChunkRecord> result in
            collection.SearchAsync(vector, top, options, cancellationToken))
        {
            results.Add(RetrievedChunk.From(result.Record, result.Score ?? 0));
        }

        return results;
    }
}

/// <summary>Rewrites conversational follow-ups into standalone search queries (Chapter 9.4).</summary>
public sealed class QueryRewriter(IChatClient smallModel)
{
    private const string Instructions = """
        Rewrite the customer's latest message as a standalone search query about Northwind Traders
        policies, using the conversation for context. Keep product types and timeframes.
        Reply with the query only.
        """;

    public async Task<string> RewriteAsync(
        IReadOnlyList<ChatMessage> history, string latestMessage, CancellationToken cancellationToken)
    {
        if (history.Count == 0)
        {
            return latestMessage;   // A first message is already standalone.
        }

        string conversation = string.Join("\n", history.TakeLast(6).Select(m => $"{m.Role}: {m.Text}"));

        ChatResponse response = await smallModel.GetResponseAsync(
            [
                new ChatMessage(ChatRole.System, Instructions),
                new ChatMessage(ChatRole.User, $"<conversation>\n{conversation}\n</conversation>\n\nLatest message: {latestMessage}")
            ],
            new ChatOptions { Temperature = 0, MaxOutputTokens = 60 },
            cancellationToken);

        string rewritten = response.Text.Trim();
        return string.IsNullOrWhiteSpace(rewritten) ? latestMessage : rewritten;
    }
}
