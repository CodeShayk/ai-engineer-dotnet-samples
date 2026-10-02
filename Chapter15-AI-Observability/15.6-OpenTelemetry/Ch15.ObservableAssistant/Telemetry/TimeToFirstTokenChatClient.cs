using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace Ch15.ObservableAssistant.Telemetry;

/// <summary>
/// Records the time to the first streamed text (Chapter 15.2), the latency users actually perceive.
/// The built-in instrumentation records the time to the first chunk of any kind
/// (gen_ai.client.operation.time_to_first_chunk), and the first chunks of a response can carry
/// no text at all, such as the role.
/// </summary>
public sealed class TimeToFirstTokenChatClient(IChatClient innerClient, Meter meter) : DelegatingChatClient(innerClient)
{
    private readonly Histogram<double> _timeToFirstToken = meter.CreateHistogram<double>(
        "northwind.ai.time_to_first_token", unit: "s", description: "Time until the first streamed text arrives");

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        long started = Stopwatch.GetTimestamp();
        bool waiting = true;

        await foreach (ChatResponseUpdate update in base.GetStreamingResponseAsync(messages, options, cancellationToken))
        {
            if (waiting && !string.IsNullOrEmpty(update.Text))
            {
                waiting = false;
                _timeToFirstToken.Record(Stopwatch.GetElapsedTime(started).TotalSeconds,
                    new KeyValuePair<string, object?>("gen_ai.request.model", options?.ModelId ?? update.ModelId));
            }

            yield return update;
        }
    }
}
