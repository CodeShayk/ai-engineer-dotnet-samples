using System.Collections.Concurrent;

namespace Ch05.StructuredPatterns;

/// <summary>
/// Stands in for the returns system's work queue. A real implementation would write to a
/// message broker or a database table; this one keeps items in memory and prints them.
/// </summary>
public sealed class ReturnsQueue
{
    private readonly ConcurrentQueue<(string EmailId, ReturnRequestDetails Details)> _items = new();

    public int Count => _items.Count;

    public Task EnqueueAsync(string emailId, ReturnRequestDetails details, CancellationToken cancellationToken = default)
    {
        _items.Enqueue((emailId, details));

        string missing = details.MissingInformation.Count == 0
            ? "nothing missing"
            : "missing: " + string.Join(", ", details.MissingInformation);

        Console.WriteLine($"Queued {emailId}: {details.DesiredOutcome,-11} {details.OrderNumber ?? "(no order)",-10} {missing}");
        return Task.CompletedTask;
    }
}
