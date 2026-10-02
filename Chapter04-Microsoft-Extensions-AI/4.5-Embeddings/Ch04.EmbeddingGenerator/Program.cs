// Chapter 4, Section 4.5: IEmbeddingGenerator, generating embeddings.
// Embeds the Northwind catalog in one batched call, then finds the products closest in
// meaning to free-text queries. Also shows the Dimensions option and an embedding cache.

using System.Diagnostics;
using System.Numerics.Tensors;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Northwind.Shared.AI;
using Northwind.Shared.Domain;

AIProviderOptions options = SampleConfiguration.LoadAIOptions();
SampleConsole.Header("Chapter 4.5: IEmbeddingGenerator", options);

IEmbeddingGenerator<string, Embedding<float>> embedder = AIClientFactory.CreateEmbeddingGenerator(options);

IReadOnlyList<Product> products = NorthwindStore.CreateSeeded().Products;

// --- One batched call for the whole catalog -----------------------------------------------
SampleConsole.Section($"Embedding {products.Count} products in one call");

long started = Stopwatch.GetTimestamp();
GeneratedEmbeddings<Embedding<float>> productVectors =
    await embedder.GenerateAsync(products.Select(p => $"{p.Name}. {p.Description}"));

Console.WriteLine($"{productVectors.Count} vectors of {productVectors[0].Vector.Length} dimensions " +
                  $"in {Stopwatch.GetElapsedTime(started).TotalMilliseconds:F0} ms");

// --- Semantic search in its simplest form -------------------------------------------------
string[] queries =
[
    "something to keep my tea hot",
    "gear for hiking in the rain",
    "a gift for someone who loves music"
];

foreach (string query in queries)
{
    SampleConsole.Section($"\"{query}\"");

    ReadOnlyMemory<float> queryVector = await embedder.GenerateVectorAsync(query);

    var matches = products
        .Zip(productVectors, (product, embedding) => new
        {
            Product = product,
            Score = TensorPrimitives.CosineSimilarity(queryVector.Span, embedding.Vector.Span)
        })
        .OrderByDescending(m => m.Score)
        .Take(3);

    foreach (var match in matches)
    {
        Console.WriteLine($"{match.Score:F3}  {match.Product.Name}");
    }
}

// --- Choosing dimensions deliberately -----------------------------------------------------
SampleConsole.Section("Shorter vectors with the Dimensions option");

if (options.Provider is AIProvider.OpenAI or AIProvider.AzureOpenAI)
{
    var embeddingOptions = new EmbeddingGenerationOptions { Dimensions = 512 };
    GeneratedEmbeddings<Embedding<float>> compact =
        await embedder.GenerateAsync(["Lumen Desk Lamp", "Aurora Wireless Earbuds"], embeddingOptions);

    Console.WriteLine($"Requested 512 dimensions, received {compact[0].Vector.Length}.");
}
else
{
    SampleConsole.Note("Configurable dimensions are a feature of models such as OpenAI's text-embedding-3 family. " +
                       "Local models like nomic-embed-text return a fixed size, so this step is skipped for Ollama.");
}

// --- Caching embeddings for repeated inputs ------------------------------------------------
SampleConsole.Section("Caching repeated inputs");

IDistributedCache distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

IEmbeddingGenerator<string, Embedding<float>> cachedEmbedder = embedder
    .AsBuilder()
    .UseDistributedCache(distributedCache)
    .Build();

for (int attempt = 1; attempt <= 2; attempt++)
{
    started = Stopwatch.GetTimestamp();
    await cachedEmbedder.GenerateVectorAsync("Is the Harbor Rain Jacket waterproof?");
    Console.WriteLine($"Attempt {attempt}: {Stopwatch.GetElapsedTime(started).TotalMilliseconds:F1} ms" +
                      (attempt == 1 ? " (calls the model)" : " (served from the cache)"));
}
