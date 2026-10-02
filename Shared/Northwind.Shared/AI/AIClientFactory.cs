using System.ClientModel;
using System.ClientModel.Primitives;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.AI;
using OllamaSharp;
using OpenAI;

namespace Northwind.Shared.AI;

/// <summary>
/// Creates provider-specific clients and hands them back as the provider-neutral
/// <see cref="IChatClient"/> and <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/>
/// abstractions. This is the only place in the companion code that knows which
/// provider is in use. Chapter 4.7 walks through the design.
/// </summary>
public static class AIClientFactory
{
    private const string AzureCognitiveServicesScope = "https://cognitiveservices.azure.com/.default";

    private static readonly Lazy<TokenCredential> AzureCredential = new(() => new DefaultAzureCredential());

    public static IChatClient CreateChatClient(AIProviderOptions options, string? model = null)
    {
        model ??= options.ChatDeployment;

        return options.Provider switch
        {
            AIProvider.Ollama => new OllamaApiClient(new Uri(options.Endpoint!), model),
            AIProvider.AzureOpenAI or AIProvider.OpenAI =>
                CreateOpenAIClient(options).GetChatClient(model).AsIChatClient(),
            _ => throw new NotSupportedException($"Provider '{options.Provider}' is not supported.")
        };
    }

    public static IEmbeddingGenerator<string, Embedding<float>> CreateEmbeddingGenerator(
        AIProviderOptions options, string? model = null)
    {
        model ??= options.EmbeddingDeployment;

        return options.Provider switch
        {
            AIProvider.Ollama => new OllamaApiClient(new Uri(options.Endpoint!), model),
            AIProvider.AzureOpenAI or AIProvider.OpenAI =>
                CreateOpenAIClient(options).GetEmbeddingClient(model).AsIEmbeddingGenerator(),
            _ => throw new NotSupportedException($"Provider '{options.Provider}' is not supported.")
        };
    }

    /// <summary>
    /// Azure OpenAI exposes an OpenAI-compatible "v1" endpoint, so the official OpenAI
    /// SDK serves both providers. Azure prefers Microsoft Entra ID tokens; an API key
    /// is used only when one is configured.
    /// </summary>
    private static OpenAIClient CreateOpenAIClient(AIProviderOptions options)
    {
        if (options.Provider == AIProvider.OpenAI)
        {
            return new OpenAIClient(new ApiKeyCredential(options.ApiKey!));
        }

        var clientOptions = new OpenAIClientOptions { Endpoint = ToAzureV1Endpoint(options.Endpoint!) };

        if (!string.IsNullOrWhiteSpace(options.ApiKey))
        {
            return new OpenAIClient(new ApiKeyCredential(options.ApiKey), clientOptions);
        }

        var tokenPolicy = new BearerTokenPolicy(AzureCredential.Value, AzureCognitiveServicesScope);
        return new OpenAIClient(tokenPolicy, clientOptions);
    }

    /// <summary>Accepts either the resource root or the full v1 URL.</summary>
    public static Uri ToAzureV1Endpoint(string endpoint)
    {
        string trimmed = endpoint.TrimEnd('/');
        if (!trimmed.EndsWith("/openai/v1", StringComparison.OrdinalIgnoreCase))
        {
            trimmed += "/openai/v1";
        }

        return new Uri(trimmed + "/");
    }
}
