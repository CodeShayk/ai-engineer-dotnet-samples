// Chapter 8, Section 8.3: Hybrid search, combining keyword and vector retrieval.
// Ranks the product catalog for each query with BM25 keyword search and with vector search,
// then merges the two rankings with Reciprocal Rank Fusion.

using System.Numerics.Tensors;
using Ch08.HybridSearch;
using Microsoft.Extensions.AI;
using Northwind.Shared.AI;
using Northwind.Shared.Domain;

AIProviderOptions options = SampleConfiguration.LoadAIOptions();
SampleConsole.Header("Chapter 8.3: Hybrid search", options);

IEmbeddingGenerator<string, Embedding<float>> embedder = AIClientFactory.CreateEmbeddingGenerator(options);
Dictionary<string, Product> products = NorthwindStore.CreateSeeded().Products.ToDictionary(p => p.ProductId);

// Index every product twice: once for keywords, once as a vector.
var keywordIndex = new Bm25Index();
foreach (Product product in products.Values)
{
    keywordIndex.Add(product.ProductId, $"{product.Name}. {product.Description}");
}

GeneratedEmbeddings<Embedding<float>> embeddings =
    await embedder.GenerateAsync(products.Values.Select(p => $"{p.Name}. {p.Description}"));
Dictionary<string, ReadOnlyMemory<float>> vectors = products.Keys
    .Zip(embeddings, (id, embedding) => (id, embedding.Vector))
    .ToDictionary(x => x.id, x => x.Vector);

string[] queries =
[
    "Is the TD-35 daypack waterproof?",
    "Does the TD-35 fit a laptop?",        // "laptop" pulls vector search toward the laptop sleeve
    "something to keep my tea hot",        // Meaning matters more than shared words
    "Summit"                               // An exact name: keyword search matches it precisely
];

const int Top = 5;

foreach (string query in queries)
{
    SampleConsole.Section($"\"{query}\"");

    List<string> keywordRanking = keywordIndex.Search(query, Top).Select(r => r.Id).ToList();

    ReadOnlyMemory<float> queryVector = await embedder.GenerateVectorAsync(query);
    List<string> vectorRanking = vectors
        .OrderByDescending(v => TensorPrimitives.CosineSimilarity(queryVector.Span, v.Value.Span))
        .Take(Top)
        .Select(v => v.Key)
        .ToList();

    List<string> fused = RankFusion.Reciprocal([keywordRanking, vectorRanking])
        .Take(Top)
        .Select(r => r.Id)
        .ToList();

    Console.WriteLine($"{"#",-3}{"Keyword (BM25)",-30}{"Vector",-30}{"Hybrid (RRF)",-30}");
    for (int rank = 0; rank < Top; rank++)
    {
        Console.WriteLine($"{rank + 1,-3}{NameAt(keywordRanking, rank),-30}{NameAt(vectorRanking, rank),-30}{NameAt(fused, rank),-30}");
    }
}

string NameAt(List<string> ranking, int rank) =>
    rank < ranking.Count ? Shorten(products[ranking[rank]].Name) : "";

static string Shorten(string name) => name.Length <= 28 ? name : name[..25] + "...";
