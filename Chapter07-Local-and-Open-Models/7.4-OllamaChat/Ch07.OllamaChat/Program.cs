// Chapter 7, Section 7.4: Connecting .NET to local models through IChatClient.
// Talks to a local Ollama server whatever AI:Provider is set to, because this chapter is
// about local inference. Override the defaults with Ollama:Endpoint and Ollama:ChatModel.
//
// 1. Checks the server, lists local models and pulls the chat model if it is missing.
// 2. Streams a response, then sets the context length with an Ollama-specific option.
// 3. Runs the Chapter 5 triage code and the Chapter 6 order tools against the local model.
// 4. Calls the same model through Ollama's OpenAI-compatible endpoint.

using System.ClientModel;
using System.Diagnostics;
using Ch05.TypedResponses;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Northwind.Shared.AI;
using Northwind.Shared.Domain;
using Northwind.Shared.Tools;
using OllamaSharp;
using OllamaSharp.Models;
using OpenAI;

IConfiguration config = SampleConfiguration.Load();
string endpoint = config["Ollama:Endpoint"] ?? "http://localhost:11434";
string modelName = config["Ollama:ChatModel"] ?? "llama3.2";

SampleConsole.Header("Chapter 7.4: Local models with Ollama");
Console.WriteLine($"Ollama endpoint: {endpoint}, chat model: {modelName}");

var ollama = new OllamaApiClient(new Uri(endpoint), modelName);

// --- Check the server is running and see which models are available locally ------------------
if (!await ollama.IsRunningAsync())
{
    Console.WriteLine("Ollama is not running. Start it and try again.");
    return;
}

SampleConsole.Section("Local models");

var localModels = (await ollama.ListLocalModelsAsync()).ToList();
foreach (var model in localModels)
{
    Console.WriteLine($"{model.Name,-30} {model.Size / 1_000_000_000.0:F1} GB");
}

// Pull the chat model if it is not present yet. Handy in setup scripts and test fixtures.
bool present = localModels.Any(m => m.Name == modelName || m.Name == $"{modelName}:latest");
if (!present)
{
    SampleConsole.Section($"Pulling {modelName}");
    await foreach (var progress in ollama.PullModelAsync(modelName))
    {
        Console.Write($"\r{progress?.Status} {progress?.Percent:F0}%   ");
    }

    Console.WriteLine();
}

// --- From here on, it is just an IChatClient -------------------------------------------------
IChatClient chatClient = ollama;

SampleConsole.Section("Streaming");

long started = Stopwatch.GetTimestamp();
await foreach (ChatResponseUpdate update in chatClient.GetStreamingResponseAsync(
    "In two sentences, explain why a customer might prefer express delivery."))
{
    Console.Write(update.Text);
}

Console.WriteLine();
SampleConsole.Note($"{Stopwatch.GetElapsedTime(started).TotalSeconds:F1} s, including loading the model if it was not already in memory.");

// --- Setting the context length ----------------------------------------------------------------
SampleConsole.Section("A larger context window for one request");

var options = new ChatOptions { Temperature = 0.2f };
options.AddOllamaOption(OllamaOption.NumCtx, 8192);

ChatResponse response = await chatClient.GetResponseAsync(
    [
        new ChatMessage(ChatRole.System, SupportPrompts.System),
        new ChatMessage(ChatRole.User, "Summarize our returns policy for unopened items in one sentence.")
    ],
    options);

Console.WriteLine(response.Text);
SampleConsole.Note($"{response.Usage?.InputTokenCount} input tokens. num_ctx was 8192 for this request only.");

// --- Structured output: the Chapter 5 triage code, unchanged ------------------------------------
SampleConsole.Section("Structured output with a local model");

string customerMessage = """
    This is the third time I'm writing. I ordered earbuds (order NW-10249) four days ago and
    the tracking hasn't changed since they shipped. I fly out on Monday and can't travel without them.
    """;

started = Stopwatch.GetTimestamp();
ChatResponse<TicketTriage> triage = await chatClient.GetResponseAsync<TicketTriage>(
    [new(ChatRole.System, "You triage customer support messages for Northwind Traders."),
     new(ChatRole.User, customerMessage)],
    new ChatOptions { Temperature = 0 });

if (triage.TryGetResult(out TicketTriage? result))
{
    Console.WriteLine($"{result.Category} | {result.Urgency} | sentiment {result.Sentiment:+0.00;-0.00} | " +
                      $"orders [{string.Join(", ", result.OrderNumbers)}]");
    Console.WriteLine(result.Summary);
}
else
{
    Console.WriteLine($"The local model's output could not be parsed: {triage.Text}");
}

SampleConsole.Note($"{Stopwatch.GetElapsedTime(started).TotalSeconds:F1} s");

// --- Tools: the Chapter 6 order tools, unchanged -------------------------------------------------
SampleConsole.Section("Tool calling with a local model");

var store = NorthwindStore.CreateSeeded();
var orderTools = new OrderTools(store, new ReturnPolicyRules(store));

IChatClient toolClient = chatClient.AsBuilder().UseFunctionInvocation().Build();

ChatResponse toolResponse = await toolClient.GetResponseAsync(
    [
        new ChatMessage(ChatRole.System, SupportPrompts.System),
        new ChatMessage(ChatRole.User, "When will order NW-10249 arrive?")
    ],
    new ChatOptions
    {
        Tools = [AIFunctionFactory.Create(orderTools.GetOrderStatus), AIFunctionFactory.Create(orderTools.CheckReturnEligibility)]
    });

Console.WriteLine(toolResponse.Text);
int calls = toolResponse.Messages.SelectMany(m => m.Contents).OfType<FunctionCallContent>().Count();
SampleConsole.Note(calls > 0
    ? $"The model made {calls} tool call(s)."
    : "The model answered without calling a tool. Smaller local models skip tools more often; try a larger tool-capable model.");

// --- The OpenAI-compatible endpoint ---------------------------------------------------------------
SampleConsole.Section("The same model through the OpenAI-compatible endpoint");

IChatClient openAICompatible = new OpenAIClient(
        new ApiKeyCredential("not-used-by-ollama"),
        new OpenAIClientOptions { Endpoint = new Uri(new Uri(endpoint), "/v1") })
    .GetChatClient(modelName)
    .AsIChatClient();

ChatResponse compatible = await openAICompatible.GetResponseAsync("Reply with a one-line greeting for a Northwind customer.");
Console.WriteLine(compatible.Text);
