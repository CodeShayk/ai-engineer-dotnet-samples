using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace Ch15.ObservableAssistant.Telemetry;

/// <summary>
/// Records time to first token for streamed responses (Chapter 15.2), the latency users
/// actually perceive. The built-in instrumentation records total duration but not this.
/// </summary>
public sealed class TimeToFirstTokenChatClient(IChatClient innerClient, Meter meter) : DelegatingChatClient(innerClient)
{
    private readonly Histogram<double> _timeToFirstToken = meter.CreateHistogram<double>(
        "northwind.ai.time_to_first_token", unit: "s", description: "Time until the first streamed update arrives");

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        long started = Stopwatch.GetTimestamp();
        bool first = true;

        await foreach (ChatResponseUpdate update in base.GetStreamingResponseAsync(messages, options, cancellationToken))
        {
            if (first)
            {
                first = false;
                _timeToFirstToken.Record(Stopwatch.GetElapsedTime(started).TotalSeconds,
                    new KeyValuePair<string, object?>("gen_ai.request.model", options?.ModelId ?? update.ModelId));
            }

            yield return update;
        }
    }
}
