using Microsoft.Extensions.AI;

namespace Northwind.Shared.Knowledge;

/// <summary>
/// Some embedding models are trained for asymmetric search and expect task prefixes on
/// queries and documents (Chapter 8.1). nomic-embed-text is the common local example.
/// </summary>
public static class EmbeddingPrefixes
{
    public static (string Document, string Query) ForModel(string embeddingModel) =>
        embeddingModel.Contains("nomic-embed", StringComparison.OrdinalIgnoreCase)
            ? ("search_document: ", "search_query: ")
            : ("", "");

    /// <summary>Wraps an embedding generator so every input is prefixed, for example for queries only.</summary>
    public static IEmbeddingGenerator<string, Embedding<float>> WithPrefix(
        this IEmbeddingGenerator<string, Embedding<float>> generator, string prefix) =>
        string.IsNullOrEmpty(prefix) ? generator : new PrefixingEmbeddingGenerator(generator, prefix);

    private sealed class PrefixingEmbeddingGenerator(IEmbeddingGenerator<string, Embedding<float>> inner, string prefix)
        : DelegatingEmbeddingGenerator<string, Embedding<float>>(inner)
    {
        public override Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
            IEnumerable<string> values, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default) =>
            base.GenerateAsync(values.Select(v => prefix + v), options, cancellationToken);
    }
}
