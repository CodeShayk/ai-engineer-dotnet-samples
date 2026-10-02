// Chapter 5, Sections 5.2 and 5.3: JSON schema and strongly typed responses.
// 1. Prints the JSON schema generated from a C# record.
// 2. Triages a support message into a TicketTriage instance with GetResponseAsync<T>.
// 3. Forces a truncated response to show the failure path with TryGetResult.
// 4. Requests a list rather than an object.

using System.Text.Json;
using Ch05.TypedResponses;
using Microsoft.Extensions.AI;
using Northwind.Shared.AI;

AIProviderOptions options = SampleConfiguration.LoadAIOptions();
SampleConsole.Header("Chapter 5.3: Typed responses", options);

IChatClient chatClient = AIClientFactory.CreateChatClient(options);
var router = new SupportRouter();

// --- 5.2: What the model receives ----------------------------------------------------------
SampleConsole.Section("The schema generated for DeliveryEstimate");

JsonElement schema = AIJsonUtilities.CreateJsonSchema(typeof(DeliveryEstimate));
Console.WriteLine(JsonSerializer.Serialize(schema, new JsonSerializerOptions { WriteIndented = true }));

// --- 5.3: A typed response ------------------------------------------------------------------
SampleConsole.Section("Triage with GetResponseAsync<TicketTriage>");

string message = """
    This is the third time I'm writing. I ordered earbuds (order NW-10249) four days ago and
    the tracking hasn't changed since they shipped. I fly out on Monday and can't travel without them.
    """;

ChatResponse<TicketTriage> response = await chatClient.GetResponseAsync<TicketTriage>(
    [
        new ChatMessage(ChatRole.System, "You triage customer support messages for Northwind Traders."),
        new ChatMessage(ChatRole.User, message)
    ],
    new ChatOptions { Temperature = 0 });

TicketTriage triage = response.Result;

Console.WriteLine($"{triage.Category} | {triage.Urgency} | sentiment {triage.Sentiment:+0.00;-0.00}");
Console.WriteLine($"Orders: {string.Join(", ", triage.OrderNumbers)}");
Console.WriteLine(triage.Summary);
Console.WriteLine();
await router.RouteAsync(triage);

// --- Reading the result safely --------------------------------------------------------------
SampleConsole.Section("The failure path: a response cut off by the output limit");

// The shared factory adds headroom for a reasoning model's hidden reasoning tokens unless the request
// sets its own reasoning options (Chapter 4.7), so set them here to keep the limit at exactly 12.
var tinyLimit = new ChatOptions { Temperature = 0, MaxOutputTokens = 12 };
if (options.IsReasoningModel(options.ChatDeployment))
{
    tinyLimit.Reasoning = new ReasoningOptions { Effort = ReasoningEffort.Low };
}

ChatResponse<TicketTriage> truncated = await chatClient.GetResponseAsync<TicketTriage>(
    [
        new ChatMessage(ChatRole.System, "You triage customer support messages for Northwind Traders."),
        new ChatMessage(ChatRole.User, message)
    ],
    tinyLimit);

if (truncated.TryGetResult(out TicketTriage? partial))
{
    Console.WriteLine($"Parsed anyway: {partial.Category}");
    await router.RouteAsync(partial);
}
else
{
    Console.WriteLine($"Triage output could not be parsed. Finish reason: {truncated.FinishReason}");
    Console.WriteLine($"Raw text: {truncated.Text}");
    await router.RouteToHumanAsync(message);
}

// --- Lists and simple values ---------------------------------------------------------------
SampleConsole.Section("A list instead of an object");

const string description =
    "The Harbor Rain Jacket is a lightweight, fully seam-sealed waterproof shell with a packable hood, " +
    "pit zips for ventilation and a recycled nylon outer.";

ChatResponse<string[]> keywords = await chatClient.GetResponseAsync<string[]>(
    $"List up to five search keywords for this product description: {description}");

foreach (string keyword in keywords.Result)
{
    Console.WriteLine($"- {keyword}");
}
