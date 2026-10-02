using Microsoft.Extensions.Configuration;

namespace Northwind.Shared.AI;

/// <summary>The model providers the companion code knows how to connect to.</summary>
public enum AIProvider
{
    Ollama,
    AzureOpenAI,
    OpenAI
}

/// <summary>
/// Strongly typed view of the "AI" configuration section shared by every sample.
/// Missing values fall back to sensible defaults for the selected provider, so a
/// machine with Ollama running locally works with no configuration at all.
/// </summary>
public sealed record AIProviderOptions
{
    public const string SectionName = "AI";

    public AIProvider Provider { get; init; } = AIProvider.Ollama;

    /// <summary>Service endpoint. Required for Azure OpenAI; defaults to localhost for Ollama.</summary>
    public string? Endpoint { get; init; }

    /// <summary>API key. Required for OpenAI; optional for Azure OpenAI, which prefers Microsoft Entra ID.</summary>
    public string? ApiKey { get; init; }

    /// <summary>Main chat model or Azure deployment name.</summary>
    public required string ChatDeployment { get; init; }

    /// <summary>A smaller, cheaper chat model for summarization, rewriting, reranking and triage.</summary>
    public required string SmallChatDeployment { get; init; }

    /// <summary>The model used as an evaluation judge (Chapter 14).</summary>
    public required string JudgeChatDeployment { get; init; }

    /// <summary>Embedding model or Azure deployment name.</summary>
    public required string EmbeddingDeployment { get; init; }

    /// <summary>Number of dimensions produced by the embedding model.</summary>
    public int EmbeddingDimensions { get; init; }

    public static AIProviderOptions FromConfiguration(IConfiguration configuration)
    {
        IConfigurationSection section = configuration.GetSection(SectionName);

        AIProvider provider = Enum.TryParse(section["Provider"], ignoreCase: true, out AIProvider parsed)
            ? parsed
            : AIProvider.Ollama;

        int? configuredDimensions = int.TryParse(section["EmbeddingDimensions"], out int d) ? d : null;

        AIProviderOptions options;
        if (provider == AIProvider.Ollama)
        {
            string chat = section["ChatDeployment"] ?? "llama3.2";
            options = new AIProviderOptions
            {
                Provider = provider,
                Endpoint = section["Endpoint"] ?? "http://localhost:11434",
                ChatDeployment = chat,
                SmallChatDeployment = section["SmallChatDeployment"] ?? chat,
                JudgeChatDeployment = section["JudgeChatDeployment"] ?? chat,
                EmbeddingDeployment = section["EmbeddingDeployment"] ?? "nomic-embed-text",
                EmbeddingDimensions = configuredDimensions ?? 768
            };
        }
        else
        {
            string chat = section["ChatDeployment"] ?? "gpt-5-mini";
            options = new AIProviderOptions
            {
                Provider = provider,
                Endpoint = section["Endpoint"],
                ApiKey = section["ApiKey"],
                ChatDeployment = chat,
                SmallChatDeployment = section["SmallChatDeployment"] ?? chat,
                JudgeChatDeployment = section["JudgeChatDeployment"] ?? chat,
                EmbeddingDeployment = section["EmbeddingDeployment"] ?? "text-embedding-3-small",
                EmbeddingDimensions = configuredDimensions ?? 1536
            };
        }

        options.Validate();
        return options;
    }

    public void Validate()
    {
        if (Provider == AIProvider.AzureOpenAI && string.IsNullOrWhiteSpace(Endpoint))
        {
            throw new InvalidOperationException(
                "AI:Endpoint must be set when AI:Provider is AzureOpenAI. Run: " +
                "dotnet user-secrets set \"AI:Endpoint\" \"https://<resource>.openai.azure.com/openai/v1/\" --id northwind-ai-engineer-samples");
        }

        if (Provider == AIProvider.OpenAI && string.IsNullOrWhiteSpace(ApiKey))
        {
            throw new InvalidOperationException(
                "AI:ApiKey must be set when AI:Provider is OpenAI. Run: " +
                "dotnet user-secrets set \"AI:ApiKey\" \"<your key>\" --id northwind-ai-engineer-samples");
        }
    }

    public override string ToString() =>
        $"{Provider} (chat: {ChatDeployment}, small: {SmallChatDeployment}, embeddings: {EmbeddingDeployment}/{EmbeddingDimensions}d)";
}
