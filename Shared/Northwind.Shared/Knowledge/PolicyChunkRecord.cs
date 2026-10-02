using Microsoft.Extensions.VectorData;

namespace Northwind.Shared.Knowledge;

/// <summary>
/// A policy chunk as stored in the vector collection, with the metadata needed for
/// filtering and citation (Chapters 9.4 and 9.5).
/// </summary>
public sealed class PolicyChunkRecord
{
    [VectorStoreKey] public required string ChunkId { get; init; }
    [VectorStoreData(IsIndexed = true)] public required string DocumentId { get; init; }
    [VectorStoreData] public required string Title { get; init; }
    [VectorStoreData] public required string Section { get; init; }
    [VectorStoreData(IsFullTextIndexed = true)] public required string Text { get; init; }

    [VectorStoreData(IsIndexed = true)] public required string Category { get; init; }
    [VectorStoreData(IsIndexed = true)] public required string Status { get; init; }     // current | archived
    [VectorStoreData(IsIndexed = true)] public required string Region { get; init; }     // global | uk | ...
    [VectorStoreData(IsIndexed = true)] public required string Audience { get; init; }   // customer | internal
    [VectorStoreData] public required string EffectiveDate { get; init; }                // ISO 8601
    [VectorStoreData] public required string ContentHash { get; init; }

    [VectorStoreVector(1536)] public ReadOnlyMemory<float>? Embedding { get; set; }

    /// <summary>
    /// The same schema as the attributes, with the vector size supplied at run time so the
    /// samples work with embedding models of any dimension (Chapter 8.6).
    /// </summary>
    public static VectorStoreCollectionDefinition CreateDefinition(int dimensions) => new()
    {
        Properties =
        [
            new VectorStoreKeyProperty(nameof(ChunkId), typeof(string)),
            new VectorStoreDataProperty(nameof(DocumentId), typeof(string)) { IsIndexed = true },
            new VectorStoreDataProperty(nameof(Title), typeof(string)),
            new VectorStoreDataProperty(nameof(Section), typeof(string)),
            new VectorStoreDataProperty(nameof(Text), typeof(string)) { IsFullTextIndexed = true },
            new VectorStoreDataProperty(nameof(Category), typeof(string)) { IsIndexed = true },
            new VectorStoreDataProperty(nameof(Status), typeof(string)) { IsIndexed = true },
            new VectorStoreDataProperty(nameof(Region), typeof(string)) { IsIndexed = true },
            new VectorStoreDataProperty(nameof(Audience), typeof(string)) { IsIndexed = true },
            new VectorStoreDataProperty(nameof(EffectiveDate), typeof(string)),
            new VectorStoreDataProperty(nameof(ContentHash), typeof(string)),
            new VectorStoreVectorProperty(nameof(Embedding), typeof(ReadOnlyMemory<float>), dimensions)
            {
                DistanceFunction = DistanceFunction.CosineSimilarity
            }
        ]
    };
}
