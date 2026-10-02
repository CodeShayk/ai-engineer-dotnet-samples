// Chapter 4, Section 4.7: Switching providers without rewriting your application.
// Runs the same questions against whichever provider is configured and prints each answer
// with its latency and token usage. Switch providers through configuration only, for example:
//
//   dotnet user-secrets set "AI:Provider" "OpenAI" --id northwind-ai-engineer-samples
//
// or, for a single run, with an environment variable (PowerShell):
//
//   $env:AI__Provider = "Ollama"; dotnet run

using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.AI;
using Northwind.Shared.AI;

CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");

AIProviderOptions options = SampleConfiguration.LoadAIOptions();
SampleConsole.Header("Chapter 4.7: Provider switching", options);

IChatClient chatClient = AIClientFactory.CreateChatClient(options);

string[] questions =
[
    "How many days do I have to return an unopened item?",
    "Classify this message as OrderStatus, ReturnsAndRefunds, Billing, ProductQuestion, AccountAndPrivacy or Other, " +
        "replying with the category only: \"Tracking says delivered but I can't find the parcel.\"",
    "Write a two-sentence product description for the Lumen Desk Lamp, a dimmable LED lamp with a USB-C charging port.",
    "My earbuds arrived three days late and I'm annoyed. What can you do?"
];

var results = new List<(TimeSpan Elapsed, long? InputTokens, long? OutputTokens)>();

foreach (string question in questions)
{
    SampleConsole.Section(question.Length > 90 ? question[..87] + "..." : question);

    long started = Stopwatch.GetTimestamp();
    ChatResponse response = await chatClient.GetResponseAsync(
        [new ChatMessage(ChatRole.System, SupportPrompts.System), new ChatMessage(ChatRole.User, question)],
        new ChatOptions { MaxOutputTokens = 400 });
    TimeSpan elapsed = Stopwatch.GetElapsedTime(started);

    Console.WriteLine(response.Text.Trim());
    SampleConsole.Note($"{elapsed.TotalSeconds:F2} s | {response.Usage?.InputTokenCount} in, " +
                       $"{response.Usage?.OutputTokenCount} out | model {response.ModelId}");

    results.Add((elapsed, response.Usage?.InputTokenCount, response.Usage?.OutputTokenCount));
}

SampleConsole.Section("Summary");
Console.WriteLine($"Provider:         {options.Provider} ({options.ChatDeployment})");
Console.WriteLine($"Average latency:  {results.Average(r => r.Elapsed.TotalSeconds):F2} s");
Console.WriteLine($"Total tokens:     {results.Sum(r => r.InputTokens ?? 0):N0} in, {results.Sum(r => r.OutputTokens ?? 0):N0} out");
SampleConsole.Note("Change AI:Provider and run again to compare. No code changes are needed.");
