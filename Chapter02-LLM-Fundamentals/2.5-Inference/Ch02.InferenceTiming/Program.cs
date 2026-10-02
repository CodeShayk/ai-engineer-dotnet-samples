// Chapter 2, Section 2.5: Inference, what happens when you call a model.
// 1. Streams a response and measures time to first token and output speed.
// 2. Sets a tiny output limit to show a response that stops with the Length finish reason.

using System.Diagnostics;
using Microsoft.Extensions.AI;
using Northwind.Shared.AI;

AIProviderOptions options = SampleConfiguration.LoadAIOptions();
SampleConsole.Header("Chapter 2.5: The anatomy of an inference request", options);

IChatClient chatClient = AIClientFactory.CreateChatClient(options);

List<ChatMessage> messages =
[
    new(ChatRole.System, "You are Northwind Assist. Answer in about 120 words."),
    new(ChatRole.User, "Explain the difference between standard and express delivery, and when each makes sense.")
];

// --- 1. Streaming: time to first token and output speed -----------------------------
SampleConsole.Section("Streaming response");

long started = Stopwatch.GetTimestamp();
TimeSpan? timeToFirstToken = null;
var updates = new List<ChatResponseUpdate>();

await foreach (ChatResponseUpdate update in chatClient.GetStreamingResponseAsync(messages))
{
    if (timeToFirstToken is null && !string.IsNullOrEmpty(update.Text))
    {
        timeToFirstToken = Stopwatch.GetElapsedTime(started);
    }

    Console.Write(update.Text);
    updates.Add(update);
}

TimeSpan total = Stopwatch.GetElapsedTime(started);
ChatResponse streamed = updates.ToChatResponse();
long? outputTokens = streamed.Usage?.OutputTokenCount;

Console.WriteLine("\n");
Console.WriteLine($"Time to first token: {timeToFirstToken?.TotalSeconds:F2} s");
Console.WriteLine($"Total time:          {total.TotalSeconds:F2} s");
if (outputTokens is > 0 && timeToFirstToken is { } ttft && total > ttft)
{
    double tokensPerSecond = outputTokens.Value / (total - ttft).TotalSeconds;
    Console.WriteLine($"Output speed:        {tokensPerSecond:F0} tokens/s ({outputTokens} output tokens)");
}
else
{
    SampleConsole.Note("The provider did not report usage for the streamed response.");
}

Console.WriteLine($"Finish reason:       {streamed.FinishReason?.Value ?? "(not reported)"}");

// --- 2. A deliberately tiny output limit ---------------------------------------------
SampleConsole.Section("Response with MaxOutputTokens = 12");

// The shared factory adds headroom for a reasoning model's hidden reasoning tokens unless the request
// sets its own reasoning options (Chapter 4.7), so set them here to keep the limit at exactly 12.
var tinyLimit = new ChatOptions { MaxOutputTokens = 12 };
if (options.IsReasoningModel(options.ChatDeployment))
{
    tinyLimit.Reasoning = new ReasoningOptions { Effort = ReasoningEffort.Low };
}

ChatResponse truncated = await chatClient.GetResponseAsync(messages, tinyLimit);

Console.WriteLine(truncated.Text);
Console.WriteLine();
Console.WriteLine($"Finish reason: {truncated.FinishReason?.Value ?? "(not reported)"}");

if (truncated.FinishReason == ChatFinishReason.Length)
{
    SampleConsole.Note("The response was cut off by the output limit. Treating it as complete would be a bug, " +
                       "especially if the output was meant to be JSON. On a reasoning model the text can be empty: " +
                       "the hidden reasoning tokens used up the limit before any visible output.");
}
