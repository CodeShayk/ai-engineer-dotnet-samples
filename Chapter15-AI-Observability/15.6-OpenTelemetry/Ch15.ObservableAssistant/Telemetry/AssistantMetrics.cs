using System.Diagnostics.Metrics;

namespace Ch15.ObservableAssistant.Telemetry;

/// <summary>
/// Behavioral metrics (Chapter 15.5): signals that no infrastructure metric captures. Tag values
/// come from small, fixed sets, never from user input.
/// </summary>
public sealed class AssistantMetrics
{
    private readonly Counter<long> _toolCalls;
    private readonly Counter<long> _retrievalMisses;
    private readonly Counter<long> _escalations;

    public AssistantMetrics(IMeterFactory meterFactory)
    {
        Meter meter = meterFactory.Create("Northwind.Assistant");
        _toolCalls = meter.CreateCounter<long>("northwind.assistant.tool_calls", description: "Tool calls by tool and outcome");
        _retrievalMisses = meter.CreateCounter<long>("northwind.assistant.retrieval_misses", description: "Questions with no relevant passages");
        _escalations = meter.CreateCounter<long>("northwind.assistant.escalations", description: "Conversations escalated to a person");
    }

    public void ToolCalled(string tool, string outcome) =>
        _toolCalls.Add(1, new("tool", tool), new("outcome", outcome));

    public void RetrievalMissed(string feature) => _retrievalMisses.Add(1, new KeyValuePair<string, object?>("feature", feature));

    public void Escalated(string reason) => _escalations.Add(1, new KeyValuePair<string, object?>("reason", reason));
}
