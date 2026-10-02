// Chapter 8, Section 8.2: Similarity search and distance metrics.
// 1. Compares cosine similarity, dot product and Euclidean distance on the product catalog.
// 2. Runs brute-force top-k search over the catalog.
// 3. Times brute-force search over 10,000 random 1,536-dimension vectors (no model needed).
//
// Usage: dotnet run [--offline]
//   --offline  runs only the timing benchmark.

using System.Diagnostics;
using System.Numerics.Tensors;
using Microsoft.Extensions.AI;
using Northwind.Shared.AI;
using Northwind.Shared.Domain;

bool offline = args.Contains("--offline");

if (!offline)
{
    AIProviderOptions options = SampleConfiguration.LoadAIOptions();
    SampleConsole.Header("Chapter 8.2: Similarity metrics", options);

    IEmbeddingGenerator<string, Embedding<float>> embedder = AIClientFactory.CreateEmbeddingGenerator(options);
    IReadOnlyList<Product> products = NorthwindStore.CreateSeeded().Products;

    GeneratedEmbeddings<Embedding<float>> productVectors =
        await embedder.GenerateAsync(products.Select(p => $"{p.Name}. {p.Description}"));

    const string Query = "waterproof clothing for walking in the rain";
    ReadOnlyMemory<float> query = await embedder.GenerateVectorAsync(Query);

    // --- Three ways to measure closeness ---------------------------------------------------------
    SampleConsole.Section($"\"{Query}\" against each product");

    Console.WriteLine($"Query vector length (L2 norm): {TensorPrimitives.Norm(query.Span):F4}");
    Console.WriteLine();
    Console.WriteLine($"{"Product",-30} {"Cosine",8} {"Dot",8} {"Euclidean",10}");

    foreach ((Product product, Embedding<float> embedding) in products.Zip(productVectors))
    {
        ReadOnlyMemory<float> document = embedding.Vector;

        float cosine    = TensorPrimitives.CosineSimilarity(query.Span, document.Span);
        float dot       = TensorPrimitives.Dot(query.Span, document.Span);
        float euclidean = TensorPrimitives.Distance(query.Span, document.Span);

        Console.WriteLine($"{product.Name,-30} {cosine,8:F3} {dot,8:F3} {euclidean,10:F3}");
    }

    SampleConsole.Note("Higher cosine and dot product mean closer; lower Euclidean distance means closer. " +
                       "If the vectors are normalized (length 1), all three produce the same ranking.");

    // --- Exact (brute-force) search ----------------------------------------------------------------
    SampleConsole.Section("Brute-force top 3");

    foreach ((int index, float score) in TopK(query, productVectors.Select(e => e.Vector).ToList(), k: 3))
    {
        Console.WriteLine($"{score:F3}  {products[index].Name}");
    }
}
else
{
    SampleConsole.Header("Chapter 8.2: Brute-force search benchmark (offline)");
}

// --- How fast is brute force? ------------------------------------------------------------------------
SampleConsole.Section("Brute force over 10,000 random 1,536-dimension vectors");

const int Count = 10_000;
const int Dimensions = 1_536;

var random = new Random(42);
List<ReadOnlyMemory<float>> vectors = Enumerable.Range(0, Count).Select(_ => RandomUnitVector(random, Dimensions)).ToList();
ReadOnlyMemory<float> probe = RandomUnitVector(random, Dimensions);

TopK(probe, vectors, k: 5).ToList();   // Warm up the JIT before timing.

const int Runs = 20;
long started = Stopwatch.GetTimestamp();
for (int run = 0; run < Runs; run++)
{
    TopK(probe, vectors, k: 5).ToList();
}

double averageMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds / Runs;
Console.WriteLine($"Average over {Runs} searches: {averageMs:F1} ms per search ({Count:N0} comparisons each).");
SampleConsole.Note("For collections up to tens of thousands of items, brute force is often entirely adequate.");

static IEnumerable<(int Index, float Score)> TopK(
    ReadOnlyMemory<float> query, IReadOnlyList<ReadOnlyMemory<float>> vectors, int k) =>
    vectors
        .Select((vector, index) => (Index: index, Score: TensorPrimitives.CosineSimilarity(query.Span, vector.Span)))
        .OrderByDescending(x => x.Score)
        .Take(k);

static ReadOnlyMemory<float> RandomUnitVector(Random random, int dimensions)
{
    float[] values = new float[dimensions];
    for (int i = 0; i < dimensions; i++)
    {
        values[i] = (float)(random.NextDouble() * 2 - 1);
    }

    TensorPrimitives.Divide(values, TensorPrimitives.Norm(values), values);
    return values;
}
