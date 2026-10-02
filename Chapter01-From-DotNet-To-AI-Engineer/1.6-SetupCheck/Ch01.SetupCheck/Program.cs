// Chapter 1, Section 1.6: Verifying your setup.
// Sends one question to whichever provider is configured and prints the answer and token usage.

using System.ClientModel.Primitives;
using Azure.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OllamaSharp;
using OpenAI;

IConfiguration config = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .AddEnvironmentVariables()
    .Build();

string provider = config["AI:Provider"] ?? "Ollama";
string endpoint = config["AI:Endpoint"]
    ?? (provider == "Ollama" ? "http://localhost:11434" : throw new InvalidOperationException("AI:Endpoint is not configured."));
string model = config["AI:ChatDeployment"]
    ?? (provider == "Ollama" ? "llama3.2" : throw new InvalidOperationException("AI:ChatDeployment is not configured."));

IChatClient chatClient = provider switch
{
    // The OpenAI SDK talks to Azure's v1 endpoint; the token policy signs each
    // request with your Microsoft Entra ID identity, so no API key is needed.
    "AzureOpenAI" => new OpenAIClient(
            new BearerTokenPolicy(new DefaultAzureCredential(),
                "https://cognitiveservices.azure.com/.default"),
            new OpenAIClientOptions { Endpoint = new Uri(endpoint) })
        .GetChatClient(model)
        .AsIChatClient(),

    "Ollama" => new OllamaApiClient(new Uri(endpoint), model),

    _ => throw new NotSupportedException($"Unknown provider '{provider}'.")
};

ChatResponse response = await chatClient.GetResponseAsync(
    "In one sentence, what is Northwind Traders best known for among .NET developers?");

Console.WriteLine($"Provider: {provider} ({model})");
Console.WriteLine(response.Text);

if (response.Usage is { } usage)
{
    Console.WriteLine($"Tokens: {usage.InputTokenCount} in, {usage.OutputTokenCount} out");
}
