// Chapter 16, Section 16.3: Resilience, rate limits and fallbacks.
// Four scenarios with fake deployments that fail on cue, so every outcome is deterministic:
//   1. Transient errors recovered by retries with backoff and jitter.
//   2. A failing primary: the circuit opens and requests fall back to a secondary deployment.
//   3. A hung deployment: the per-attempt timeout fires and the request falls back.
//   4. Streaming: fallback happens before the first update, never after.
// The timeouts and delays are shortened from the book's values so the demo runs in seconds.

using System.Diagnostics;
using Ch16.Resilience;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Northwind.Shared.AI;
using Polly;

SampleConsole.Header("Chapter 16.3: Resilient model access");

using ILoggerFactory loggerFactory = SampleConsole.CreateLoggerFactory();
List<ChatMessage> question = [new(ChatRole.User, "Where is my order NW-10249?")];

ResiliencePipeline DemoPipeline(string deployment, int minimumThroughput = 2) => ModelResilience.CreatePipeline(
    overallTimeout: TimeSpan.FromSeconds(10),
    attemptTimeout: TimeSpan.FromSeconds(2),
    retryDelay: TimeSpan.FromMilliseconds(200),
    minimumThroughput: minimumThroughput,
    onEvent: message => SampleConsole.Note($"  [{deployment}] {message}"));

// --- 1. Retries ------------------------------------------------------------------------------------
SampleConsole.Section("1. Two transient failures, then success");
{
    var flaky = new FlakyChatClient("primary-eastus", FailureMode.ServerError, failures: 2);
    using var client = new ResilientChatClient(flaky, DemoPipeline("primary-eastus", minimumThroughput: 10));

    ChatResponse response = await client.GetResponseAsync(question);
    Console.WriteLine($"{response.Text} after {flaky.Calls} attempts.");
}

// --- 2. Circuit breaker and fallback ----------------------------------------------------------------
SampleConsole.Section("2. A primary that keeps failing: the circuit opens, the secondary answers");
{
    var primary = new FlakyChatClient("primary-eastus", FailureMode.RateLimited);
    var secondary = new FlakyChatClient("secondary-westeurope", FailureMode.None);

    using var client = new FallbackChatClient(
        [new ResilientChatClient(primary, DemoPipeline("primary-eastus")), new ResilientChatClient(secondary, DemoPipeline("secondary-westeurope"))],
        loggerFactory.CreateLogger<FallbackChatClient>());

    for (int request = 1; request <= 3; request++)
    {
        ChatResponse response = await client.GetResponseAsync(question);
        Console.WriteLine($"Request {request}: {response.Text}. Primary attempts so far: {primary.Calls}.");
    }

    SampleConsole.Note("Once the circuit is open, the primary is not called at all until the break duration passes.");
}

// --- 3. Timeouts -----------------------------------------------------------------------------------
SampleConsole.Section("3. A deployment that hangs: the per-attempt timeout fires");
{
    var hung = new FlakyChatClient("primary-eastus", FailureMode.Hang);
    var secondary = new FlakyChatClient("secondary-westeurope", FailureMode.None);

    using var client = new FallbackChatClient(
        [new ResilientChatClient(hung, DemoPipeline("primary-eastus", minimumThroughput: 10)), secondary],
        loggerFactory.CreateLogger<FallbackChatClient>());

    long started = Stopwatch.GetTimestamp();
    ChatResponse response = await client.GetResponseAsync(question);
    Console.WriteLine($"{response.Text} after {Stopwatch.GetElapsedTime(started).TotalSeconds:F1} s.");
    SampleConsole.Note("Hangs are not retried here, because a timeout is not one of the transient errors the retry handles.");
}

// --- 4. Streaming --------------------------------------------------------------------------------------
SampleConsole.Section("4. Streaming falls back only before the first update");
{
    var primary = new FlakyChatClient("primary-eastus", FailureMode.ServerError);
    var secondary = new FlakyChatClient("secondary-westeurope", FailureMode.None);

    using var client = new FallbackChatClient([primary, secondary], loggerFactory.CreateLogger<FallbackChatClient>());

    await foreach (ChatResponseUpdate update in client.GetStreamingResponseAsync(question))
    {
        Console.Write(update.Text);
    }

    Console.WriteLine();
}
