using System.ClientModel;
using System.ClientModel.Primitives;
using Azure.Identity;
using Microsoft.Extensions.AI;
using OllamaSharp;
using OpenAI;

namespace PortfolioStarter.AI;

/// <summary>Which provider serves the models, read from the "AI" configuration section.</summary>
public sealed class ModelOptions
{
    public string Provider { get; set; } = "Ollama";   // Ollama | AzureOpenAI | OpenAI
    public string? Endpoint { get; set; }
    public string? ApiKey { get; set; }
    public string? ChatDeployment { get; set; }

    public string ChatModel => ChatDeployment ?? (Provider == "Ollama" ? "llama3.2" : "gpt-5-mini");

    public string ResolvedEndpoint => Endpoint ?? (Provider == "Ollama" ? "http://localhost:11434" : "");

    /// <summary>Comma-separated deployments that serve reasoning models, when their names do not say so.</summary>
    public string? ReasoningDeployments { get; set; }

    /// <summary>
    /// Reasoning models, such as the gpt-5 family and the o-series, reject sampling settings like
    /// temperature with HTTP 400, and count hidden reasoning tokens against the output limit.
    /// Names that reveal the model are recognized without being listed.
    /// </summary>
    public bool IsReasoningModel =>
        Provider is "AzureOpenAI" or "OpenAI"
        && (ChatModel.StartsWith("gpt-5", StringComparison.OrdinalIgnoreCase)
            || (ChatModel.Length > 1 && char.ToLowerInvariant(ChatModel[0]) == 'o' && char.IsDigit(ChatModel[1]))
            || (ReasoningDeployments ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Contains(ChatModel, StringComparer.OrdinalIgnoreCase));

    public void Validate()
    {
        if (Provider is not ("Ollama" or "AzureOpenAI" or "OpenAI"))
        {
            throw new InvalidOperationException($"AI:Provider '{Provider}' is not supported. Use Ollama, AzureOpenAI or OpenAI.");
        }

        if (Provider == "AzureOpenAI" && string.IsNullOrWhiteSpace(Endpoint))
        {
            throw new InvalidOperationException("AI:Endpoint is required for Azure OpenAI, for example https://<resource>.openai.azure.com/openai/v1/");
        }

        if (Provider == "OpenAI" && string.IsNullOrWhiteSpace(ApiKey))
        {
            throw new InvalidOperationException("AI:ApiKey is required for OpenAI.");
        }
    }
}

/// <summary>
/// The only place that knows about provider SDKs (Chapter 4.7). Everything else depends on
/// IChatClient, so switching providers is a configuration change.
/// </summary>
public static class ModelClients
{
    public static IChatClient CreateChatClient(ModelOptions options)
    {
        options.Validate();

        IChatClient client = options.Provider switch
        {
            "Ollama" => new OllamaApiClient(new Uri(options.ResolvedEndpoint), options.ChatModel),
            "OpenAI" => new OpenAIClient(new ApiKeyCredential(options.ApiKey!)).GetChatClient(options.ChatModel).AsIChatClient(),
            _ => CreateAzureOpenAIClient(options).GetChatClient(options.ChatModel).AsIChatClient()
        };

        // Remove the sampling settings that reasoning models reject, so features can set them freely, and
        // unless a feature chooses its own reasoning options, ask for low effort and add headroom to any
        // output limit for the hidden reasoning tokens.
        return options.IsReasoningModel
            ? client.AsBuilder()
                .ConfigureOptions(o =>
                {
                    o.Temperature = null;
                    o.TopP = null;
                    o.FrequencyPenalty = null;
                    o.PresencePenalty = null;

                    if (o.Reasoning is null)
                    {
                        o.Reasoning = new ReasoningOptions { Effort = ReasoningEffort.Low };
                        o.MaxOutputTokens += 1_024;
                    }
                })
                .Build()
            : client;
    }

    private static OpenAIClient CreateAzureOpenAIClient(ModelOptions options)
    {
        string endpoint = options.Endpoint!.TrimEnd('/');
        var clientOptions = new OpenAIClientOptions
        {
            Endpoint = new Uri(endpoint.EndsWith("/openai/v1", StringComparison.OrdinalIgnoreCase) ? endpoint + "/" : endpoint + "/openai/v1/")
        };

        // Prefer Microsoft Entra ID; fall back to an API key only when one is configured.
        return string.IsNullOrWhiteSpace(options.ApiKey)
            ? new OpenAIClient(new BearerTokenPolicy(new DefaultAzureCredential(), "https://cognitiveservices.azure.com/.default"), clientOptions)
            : new OpenAIClient(new ApiKeyCredential(options.ApiKey), clientOptions);
    }
}
