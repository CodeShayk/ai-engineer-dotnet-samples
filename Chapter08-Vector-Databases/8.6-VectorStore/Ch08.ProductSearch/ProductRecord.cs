using Microsoft.Extensions.VectorData;

namespace Ch08.ProductSearch;

/// <summary>A product as stored in the vector collection (Section 8.4).</summary>
public sealed class ProductRecord
{
    [VectorStoreKey]
    public required string ProductId { get; init; }

    [VectorStoreData(IsFullTextIndexed = true)]
    public required string Name { get; init; }

    [VectorStoreData(IsFullTextIndexed = true)]
    public required string Description { get; init; }

    [VectorStoreData(IsIndexed = true)]
    public required string Category { get; init; }

    [VectorStoreData(IsIndexed = true)]
    public double Price { get; init; }

    [VectorStoreVector(1536, DistanceFunction = DistanceFunction.CosineSimilarity, IndexKind = IndexKind.Hnsw)]
    public ReadOnlyMemory<float>? Embedding { get; set; }
}

/// <summary>
/// The same product for the automatic style: the vector property is a string, and the store
/// embeds it with the configured IEmbeddingGenerator on upsert and at query time.
/// </summary>
public sealed class ProductTextRecord
{
    [VectorStoreKey]
    public required string ProductId { get; init; }

    [VectorStoreData]
    public required string Name { get; init; }

    [VectorStoreData(IsIndexed = true)]
    public required string Category { get; init; }

    [VectorStoreData(IsIndexed = true)]
    public double Price { get; init; }

    [VectorStoreVector(1536, DistanceFunction = DistanceFunction.CosineSimilarity)]
    public required string SearchText { get; init; }
}
