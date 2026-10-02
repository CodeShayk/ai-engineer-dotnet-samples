using System.Numerics.Tensors;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;

namespace Ch13.SecureRetrieval;

/// <summary>
/// A tiny bag-of-words embedding generator for --offline runs. Words are hashed into a fixed
/// number of dimensions, so texts that share words end up close together. It understands no
/// meaning at all, but it is enough to show that the filters, not the similarity, decide what a
/// caller can see.
/// </summary>
public sealed partial class HashingEmbeddingGenerator(int dimensions = 256) : IEmbeddingGenerator<string, Embedding<float>>
{
    [GeneratedRegex("[a-z0-9]+")]
    private static partial Regex Word();

    public int Dimensions => dimensions;

    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(new GeneratedEmbeddings<Embedding<float>>(values.Select(v => new Embedding<float>(Embed(v)))));

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }

    private float[] Embed(string text)
    {
        float[] vector = new float[dimensions];
        foreach (Match word in Word().Matches(text.ToLowerInvariant()))
        {
            vector[Fnv1a(word.Value) % (uint)dimensions] += 1;
        }

        float norm = TensorPrimitives.Norm(vector);
        if (norm > 0)
        {
            TensorPrimitives.Divide(vector, norm, vector);
        }

        return vector;
    }

    // A stable hash, unlike string.GetHashCode, which changes between processes.
    private static uint Fnv1a(string value)
    {
        uint hash = 2166136261;
        foreach (char c in value)
        {
            hash = (hash ^ c) * 16777619;
        }

        return hash;
    }
}
