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

    /// <param name="options">The provider configuration.</param>
    /// <param name="model">The model or deployment; defaults to the main chat deployment.</param>
    /// <param name="sdkRetries">
    /// False turns off the OpenAI SDK's own retries, for callers that retry in a resilience pipeline
    /// of their own (Chapter 16.3), so that failures are not retried in two places.
    /// </param>
    public static IChatClient CreateChatClient(AIProviderOptions options, string? model = null, bool sdkRetries = true)
    {
        model ??= options.ChatDeployment;

        IChatClient client = options.Provider switch
        {
            AIProvider.Ollama => new OllamaApiClient(new Uri(options.Endpoint!), model),
            AIProvider.AzureOpenAI or AIProvider.OpenAI =>
                CreateOpenAIClient(options, sdkRetries).GetChatClient(model).AsIChatClient(),
            _ => throw new NotSupportedException($"Provider '{options.Provider}' is not supported.")
        };

        // Reasoning models, such as the gpt-5 family, reject sampling settings like temperature
        // with HTTP 400, and their hidden reasoning tokens count against the output limit.
        // Adapting the request here lets the same calling code run against any model.
        return options.IsReasoningModel(model) ? ForReasoningModel(client) : client;
    }

    /// <summary>Headroom added to a request's output limit for a reasoning model's hidden reasoning tokens.</summary>
    public const int ReasoningTokenAllowance = 1_024;

    /// <summary>
    /// Wraps a client for a reasoning model. Temperature, top-p and the repetition penalties are removed,
    /// because reasoning models reject them. Unless the request sets its own reasoning options, low reasoning
    /// effort is requested and any output limit is raised by <see cref="ReasoningTokenAllowance"/>, because
    /// hidden reasoning tokens count against the limit: a limit sized for a one-word answer could otherwise be
    /// used up before any visible text is produced. The caller's options are not modified.
    /// </summary>
    public static IChatClient ForReasoningModel(IChatClient client) =>
        client.AsBuilder()
            .ConfigureOptions(options =>
            {
                options.Temperature = null;
                options.TopP = null;
                options.FrequencyPenalty = null;
                options.PresencePenalty = null;

                if (options.Reasoning is null)
                {
                    options.Reasoning = new ReasoningOptions { Effort = ReasoningEffort.Low };
                    options.MaxOutputTokens += ReasoningTokenAllowance;   // Stays null when no limit was set.
                }
            })
            .Build();

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
    private static OpenAIClient CreateOpenAIClient(AIProviderOptions options, bool sdkRetries = true)
    {
        // The SDK retries transient failures itself unless the caller does its own retrying.
        var clientOptions = new OpenAIClientOptions();
        if (!sdkRetries)
        {
            clientOptions.RetryPolicy = new ClientRetryPolicy(maxRetries: 0);
        }

        if (options.Provider == AIProvider.OpenAI)
        {
            return new OpenAIClient(new ApiKeyCredential(options.ApiKey!), clientOptions);
        }

        clientOptions.Endpoint = ToAzureV1Endpoint(options.Endpoint!);

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
