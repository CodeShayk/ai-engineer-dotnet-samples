using CommunityToolkit.VectorData.InMemory;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using Northwind.Shared.AI;

namespace Northwind.Shared.Knowledge;

/// <summary>
/// Convenience wiring for the samples: an in-memory vector collection loaded with the
/// policy library, plus a retriever over it. Production code would use a real vector
/// store and run ingestion separately (Chapter 9.9).
/// </summary>
public sealed class PolicyKnowledgeBase
{
    private PolicyKnowledgeBase(VectorStoreCollection<string, PolicyChunkRecord> collection, PolicyRetriever retriever, int chunkCount)
    {
        Collection = collection;
        Retriever = retriever;
        ChunkCount = chunkCount;
    }

    public VectorStoreCollection<string, PolicyChunkRecord> Collection { get; }

    public PolicyRetriever Retriever { get; }

    public int ChunkCount { get; }

    public static async Task<PolicyKnowledgeBase> CreateInMemoryAsync(
        AIProviderOptions options,
        IEmbeddingGenerator<string, Embedding<float>> embedder,
        IEnumerable<PolicyDocument>? documents = null,
        CancellationToken cancellationToken = default)
    {
        (string documentPrefix, string queryPrefix) = EmbeddingPrefixes.ForModel(options.EmbeddingDeployment);

        var store = new InMemoryVectorStore();
        VectorStoreCollection<string, PolicyChunkRecord> collection = store.GetCollection<string, PolicyChunkRecord>(
            "northwind-policies", PolicyChunkRecord.CreateDefinition(options.EmbeddingDimensions));

        var ingestor = new PolicyIngestor(collection, embedder.WithPrefix(documentPrefix), MarkdownSectionChunker.CreateDefault());
        int chunks = await ingestor.IngestAsync(documents ?? PolicyLibrary.LoadAll(), cancellationToken);

        return new PolicyKnowledgeBase(collection, new PolicyRetriever(collection, embedder.WithPrefix(queryPrefix)), chunks);
    }
}
