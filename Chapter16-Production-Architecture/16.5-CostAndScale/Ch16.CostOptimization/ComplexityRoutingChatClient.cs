using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace Ch16.CostOptimization;

/// <summary>
/// Sends simple requests to a small model and complex ones to a larger model (Chapter 16.5).
/// Cheap heuristics decide most requests; a fast classification by the small model decides only
/// the ambiguous ones. The decisions are counted so the share of traffic per model can be reported.
/// </summary>
public sealed class ComplexityRoutingChatClient(IChatClient smallClient, IChatClient largeClient) : IChatClient
{
    private static readonly string[] ComplexSignals =
        ["compare", "explain why", "complaint", "unacceptable", "angry", "legal", "several", "both", "step by step", "and also"];

    public ConcurrentDictionary<string, int> Decisions { get; } = new();

    /// <summary>The most recent routing decision, for display.</summary>
    public string? LastRoute { get; private set; }

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        List<ChatMessage> request = [.. messages];
        IChatClient target = await SelectAsync(request, options, cancellationToken);
        return await target.GetResponseAsync(request, options, cancellationToken);
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        List<ChatMessage> request = [.. messages];
        IChatClient target = await SelectAsync(request, options, cancellationToken);

        await foreach (ChatResponseUpdate update in target.GetStreamingResponseAsync(request, options, cancellationToken))
        {
            yield return update;
        }
    }

    private async Task<IChatClient> SelectAsync(List<ChatMessage> messages, ChatOptions? options, CancellationToken cancellationToken)
    {
        string latest = messages.LastOrDefault(m => m.Role == ChatRole.User)?.Text ?? "";
        string route = Heuristic(messages, options, latest) ?? await ClassifyAsync(latest, cancellationToken);

        Decisions.AddOrUpdate(route, 1, (_, count) => count + 1);
        LastRoute = route;
        return route.StartsWith("large", StringComparison.Ordinal) ? largeClient : smallClient;
    }

    private static string? Heuristic(List<ChatMessage> messages, ChatOptions? options, string latest)
    {
        if (options?.Tools is { Count: > 0 })
        {
            return "large (tools)";                      // Multi-step tool use needs the stronger model
        }

        if (messages.Count(m => m.Role == ChatRole.User) > 4)
        {
            return "large (long conversation)";          // Long conversations are harder to keep straight
        }

        if (ComplexSignals.Any(s => latest.Contains(s, StringComparison.OrdinalIgnoreCase)) || latest.Length > 300)
        {
            return "large (complex wording)";
        }

        if (latest.Length < 80 && latest.Count(c => c == '?') <= 1)
        {
            return "small (short, single question)";
        }

        return null;                                     // Ambiguous: ask the small model
    }

    private async Task<string> ClassifyAsync(string latest, CancellationToken cancellationToken)
    {
        ChatResponse response = await smallClient.GetResponseAsync(
            [
                new ChatMessage(ChatRole.System,
                    "Classify the customer message as SIMPLE (one factual question about a policy, delivery or order) " +
                    "or COMPLEX (several questions, a complaint, a comparison or anything needing judgment). " +
                    "Reply with one word: SIMPLE or COMPLEX."),
                new ChatMessage(ChatRole.User, latest)
            ],
            new ChatOptions { Temperature = 0, MaxOutputTokens = 5 },
            cancellationToken);

        return response.Text.Contains("COMPLEX", StringComparison.OrdinalIgnoreCase)
            ? "large (classified complex)"
            : "small (classified simple)";
    }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceType.IsInstanceOfType(this) ? this : largeClient.GetService(serviceType, serviceKey);

    public void Dispose()
    {
        smallClient.Dispose();
        largeClient.Dispose();
    }
}
