using Microsoft.Extensions.VectorData;

namespace Ch13.SecureRetrieval;

/// <summary>
/// A chunk in a multi-tenant knowledge base, with the metadata needed for mandatory filtering
/// and provenance metadata that lets suspicious content be traced and removed (Chapter 13.6).
/// </summary>
public sealed class KnowledgeChunkRecord
{
    [VectorStoreKey] public required string ChunkId { get; init; }
    [VectorStoreData(IsIndexed = true)] public required string TenantId { get; init; }
    [VectorStoreData(IsIndexed = true)] public required string Audience { get; init; }   // customer | internal
    [VectorStoreData(IsIndexed = true)] public required string Status { get; init; }     // current | archived
    [VectorStoreData] public required string Title { get; init; }
    [VectorStoreData] public required string Text { get; init; }

    // Provenance: where the content came from, who added it and when.
    [VectorStoreData] public required string Source { get; init; }
    [VectorStoreData] public required string AddedBy { get; init; }
    [VectorStoreData] public required string AddedAt { get; init; }

    [VectorStoreVector(1536)] public ReadOnlyMemory<float>? Embedding { get; set; }

    /// <summary>The same schema with the vector size supplied at run time.</summary>
    public static VectorStoreCollectionDefinition CreateDefinition(int dimensions) => new()
    {
        Properties =
        [
            new VectorStoreKeyProperty(nameof(ChunkId), typeof(string)),
            new VectorStoreDataProperty(nameof(TenantId), typeof(string)) { IsIndexed = true },
            new VectorStoreDataProperty(nameof(Audience), typeof(string)) { IsIndexed = true },
            new VectorStoreDataProperty(nameof(Status), typeof(string)) { IsIndexed = true },
            new VectorStoreDataProperty(nameof(Title), typeof(string)),
            new VectorStoreDataProperty(nameof(Text), typeof(string)),
            new VectorStoreDataProperty(nameof(Source), typeof(string)),
            new VectorStoreDataProperty(nameof(AddedBy), typeof(string)),
            new VectorStoreDataProperty(nameof(AddedAt), typeof(string)),
            new VectorStoreVectorProperty(nameof(Embedding), typeof(ReadOnlyMemory<float>), dimensions)
            {
                DistanceFunction = DistanceFunction.CosineSimilarity
            }
        ]
    };
}
