// Chapter 8, Section 8.6: Modeling, indexing and querying vector data in C#.
// 1. Creates a collection from a run-time schema, so the vector size comes from configuration.
// 2. Loads the product catalog: embeds it in one batch and upserts the records.
// 3. Searches with and without a filter.
// 4. Reads and deletes records by key.
// 5. Repeats the search with the automatic style, where the store generates the embeddings.

using Ch08.ProductSearch;
using CommunityToolkit.VectorData.InMemory;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using Northwind.Shared.AI;
using Northwind.Shared.Domain;

AIProviderOptions options = SampleConfiguration.LoadAIOptions();
SampleConsole.Header("Chapter 8.6: Product search with Microsoft.Extensions.VectorData", options);

IEmbeddingGenerator<string, Embedding<float>> embedder = AIClientFactory.CreateEmbeddingGenerator(options);

// --- Creating a collection with a run-time schema ------------------------------------------------
var definition = new VectorStoreCollectionDefinition
{
    Properties =
    [
        new VectorStoreKeyProperty(nameof(ProductRecord.ProductId), typeof(string)),
        new VectorStoreDataProperty(nameof(ProductRecord.Name), typeof(string)) { IsFullTextIndexed = true },
        new VectorStoreDataProperty(nameof(ProductRecord.Description), typeof(string)) { IsFullTextIndexed = true },
        new VectorStoreDataProperty(nameof(ProductRecord.Category), typeof(string)) { IsIndexed = true },
        new VectorStoreDataProperty(nameof(ProductRecord.Price), typeof(double)) { IsIndexed = true },
        new VectorStoreVectorProperty(nameof(ProductRecord.Embedding), typeof(ReadOnlyMemory<float>), options.EmbeddingDimensions)
        {
            DistanceFunction = DistanceFunction.CosineSimilarity
        }
    ]
};

VectorStore store = new InMemoryVectorStore();
VectorStoreCollection<string, ProductRecord> products = store.GetCollection<string, ProductRecord>("products", definition);

await products.EnsureCollectionExistsAsync();

// --- Loading data --------------------------------------------------------------------------------
SampleConsole.Section("Loading the catalog");

IReadOnlyList<Product> catalog = NorthwindStore.CreateSeeded().Products;

GeneratedEmbeddings<Embedding<float>> vectors =
    await embedder.GenerateAsync(catalog.Select(p => $"{p.Name}. {p.Description}"));

IEnumerable<ProductRecord> records = catalog.Zip(vectors, (product, vector) => new ProductRecord
{
    ProductId = product.ProductId,
    Name = product.Name,
    Description = product.Description,
    Category = product.Category.ToString(),
    Price = (double)product.Price,
    Embedding = vector.Vector
});

await products.UpsertAsync(records);
Console.WriteLine($"Upserted {catalog.Count} products with {options.EmbeddingDimensions}-dimension vectors.");

// --- Searching with and without filters ----------------------------------------------------------
const string Query = "something to listen to music while running";
ReadOnlyMemory<float> queryVector = await embedder.GenerateVectorAsync(Query);

SampleConsole.Section($"\"{Query}\" with no filter");

await foreach (VectorSearchResult<ProductRecord> result in products.SearchAsync(queryVector, top: 3))
{
    Console.WriteLine($"{result.Score:F3}  {result.Record.Name,-28} ${result.Record.Price}  ({result.Record.Category})");
}

SampleConsole.Section("The same query: electronics under $150");

var searchOptions = new VectorSearchOptions<ProductRecord>
{
    Filter = p => p.Category == "Electronics" && p.Price <= 150
};

await foreach (VectorSearchResult<ProductRecord> result in products.SearchAsync(queryVector, top: 3, searchOptions))
{
    Console.WriteLine($"{result.Score:F3}  {result.Record.Name,-28} ${result.Record.Price}");
}

SampleConsole.Note("The Orbit Fitness Watch ($199) is excluded by the filter before ranking, so only two results qualify.");

// --- Reading, updating and deleting -------------------------------------------------------------
SampleConsole.Section("Reading and deleting by key");

ProductRecord? earbuds = await products.GetAsync("P01");
Console.WriteLine($"GetAsync(\"P01\"): {earbuds?.Name} (${earbuds?.Price})");

await products.DeleteAsync("P10");   // A discontinued product
ProductRecord? deleted = await products.GetAsync("P10");
Console.WriteLine($"After DeleteAsync(\"P10\"): {(deleted is null ? "not found" : deleted.Name)}");

// --- Letting the store generate embeddings ------------------------------------------------------
SampleConsole.Section("The automatic style: the store embeds strings for you");

var textDefinition = new VectorStoreCollectionDefinition
{
    Properties =
    [
        new VectorStoreKeyProperty(nameof(ProductTextRecord.ProductId), typeof(string)),
        new VectorStoreDataProperty(nameof(ProductTextRecord.Name), typeof(string)),
        new VectorStoreDataProperty(nameof(ProductTextRecord.Category), typeof(string)) { IsIndexed = true },
        new VectorStoreDataProperty(nameof(ProductTextRecord.Price), typeof(double)) { IsIndexed = true },
        new VectorStoreVectorProperty(nameof(ProductTextRecord.SearchText), typeof(string), options.EmbeddingDimensions)
        {
            DistanceFunction = DistanceFunction.CosineSimilarity
        }
    ]
};

VectorStore autoStore = new InMemoryVectorStore(new InMemoryVectorStoreOptions { EmbeddingGenerator = embedder });
VectorStoreCollection<string, ProductTextRecord> textProducts =
    autoStore.GetCollection<string, ProductTextRecord>("products-text", textDefinition);

await textProducts.EnsureCollectionExistsAsync();

// No embedding code: the connector embeds SearchText on upsert...
await textProducts.UpsertAsync(catalog.Select(p => new ProductTextRecord
{
    ProductId = p.ProductId,
    Name = p.Name,
    Category = p.Category.ToString(),
    Price = (double)p.Price,
    SearchText = $"{p.Name}. {p.Description}"
}));

// ...and embeds the query string with the same model.
await foreach (VectorSearchResult<ProductTextRecord> result in textProducts.SearchAsync(Query, top: 3))
{
    Console.WriteLine($"{result.Score:F3}  {result.Record.Name,-28} ${result.Record.Price}");
}
