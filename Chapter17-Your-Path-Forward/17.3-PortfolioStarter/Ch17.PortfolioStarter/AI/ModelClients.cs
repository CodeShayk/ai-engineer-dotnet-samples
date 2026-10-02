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

        return options.Provider switch
        {
            "Ollama" => new OllamaApiClient(new Uri(options.ResolvedEndpoint), options.ChatModel),
            "OpenAI" => new OpenAIClient(new ApiKeyCredential(options.ApiKey!)).GetChatClient(options.ChatModel).AsIChatClient(),
            _ => CreateAzureOpenAIClient(options).GetChatClient(options.ChatModel).AsIChatClient()
        };
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
