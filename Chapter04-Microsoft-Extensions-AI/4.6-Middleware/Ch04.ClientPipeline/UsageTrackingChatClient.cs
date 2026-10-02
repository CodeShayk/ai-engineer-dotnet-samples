using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Ch04.ClientPipeline;

/// <summary>Logs the latency and token usage of every model call that reaches it (Chapter 4.6).</summary>
public sealed class UsageTrackingChatClient(IChatClient innerClient, ILogger<UsageTrackingChatClient> logger)
    : DelegatingChatClient(innerClient)
{
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        long started = Stopwatch.GetTimestamp();
        ChatResponse response = await base.GetResponseAsync(messages, options, cancellationToken);

        LogUsage(response.ModelId, response.Usage, Stopwatch.GetElapsedTime(started));
        return response;
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        long started = Stopwatch.GetTimestamp();
        string? modelId = null;
        UsageDetails? usage = null;

        await foreach (ChatResponseUpdate update in base.GetStreamingResponseAsync(messages, options, cancellationToken))
        {
            modelId ??= update.ModelId;
            foreach (UsageContent usageContent in update.Contents.OfType<UsageContent>())
            {
                usage = usageContent.Details;
            }

            yield return update;
        }

        LogUsage(modelId, usage, Stopwatch.GetElapsedTime(started));
    }

    private void LogUsage(string? modelId, UsageDetails? usage, TimeSpan elapsed) =>
        logger.LogInformation(
            "Model {ModelId} responded in {ElapsedMs:F0} ms using {InputTokens} input and {OutputTokens} output tokens",
            modelId, elapsed.TotalMilliseconds, usage?.InputTokenCount, usage?.OutputTokenCount);
}
