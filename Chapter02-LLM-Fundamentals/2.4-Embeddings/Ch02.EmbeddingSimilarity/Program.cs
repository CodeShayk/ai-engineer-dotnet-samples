// Chapter 2, Section 2.4: Embeddings, representing meaning as vectors.
// Embeds three sentences and compares every pair with cosine similarity.

using System.Numerics.Tensors;
using Microsoft.Extensions.AI;
using Northwind.Shared.AI;

AIProviderOptions options = SampleConfiguration.LoadAIOptions();
SampleConsole.Header("Chapter 2.4: Meaning as distance", options);

IEmbeddingGenerator<string, Embedding<float>> embedder =
    AIClientFactory.CreateEmbeddingGenerator(options);

string[] sentences =
[
    "How do I send back a jacket that doesn't fit?",
    "What is your returns process for clothing?",
    "When will my order of garden furniture arrive?"
];

GeneratedEmbeddings<Embedding<float>> embeddings = await embedder.GenerateAsync(sentences);

Console.WriteLine($"Each sentence became a vector of {embeddings[0].Vector.Length} numbers.\n");

for (int i = 0; i < sentences.Length; i++)
{
    for (int j = i + 1; j < sentences.Length; j++)
    {
        float similarity = TensorPrimitives.CosineSimilarity(
            embeddings[i].Vector.Span,
            embeddings[j].Vector.Span);

        Console.WriteLine($"{similarity:F2}  \"{sentences[i]}\" <-> \"{sentences[j]}\"");
    }
}

SampleConsole.Note("\nThe two returns questions share almost no words, yet they score highest. " +
                   "Absolute values vary by embedding model; compare relative order, not fixed thresholds.");
