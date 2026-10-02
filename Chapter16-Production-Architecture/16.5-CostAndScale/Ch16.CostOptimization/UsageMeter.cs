using System.Collections.Concurrent;
using Microsoft.Extensions.AI;

namespace Ch16.CostOptimization;

/// <summary>Illustrative prices per million tokens for a model tier. Replace with your provider's rates.</summary>
public sealed record TierPrice(double InputPerMillion, double OutputPerMillion);

/// <summary>Records token usage per tier as calls pass through, for the cost report.</summary>
public sealed class UsageMeter
{
    public ConcurrentDictionary<string, (long Input, long Output, int Calls)> Usage { get; } = new();

    public IChatClient Wrap(IChatClient client, string tier) =>
        new MeteredChatClient(client, tier, this);

    public double Cost(IReadOnlyDictionary<string, TierPrice> prices) =>
        Usage.Sum(u => u.Value.Input * prices[u.Key].InputPerMillion / 1_000_000 + u.Value.Output * prices[u.Key].OutputPerMillion / 1_000_000);

    internal void Record(string tier, UsageDetails? usage) =>
        Usage.AddOrUpdate(tier,
            (usage?.InputTokenCount ?? 0, usage?.OutputTokenCount ?? 0, 1),
            (_, current) => (current.Input + (usage?.InputTokenCount ?? 0), current.Output + (usage?.OutputTokenCount ?? 0), current.Calls + 1));

    private sealed class MeteredChatClient(IChatClient inner, string tier, UsageMeter meter) : DelegatingChatClient(inner)
    {
        public override async Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            ChatResponse response = await base.GetResponseAsync(messages, options, cancellationToken);
            meter.Record(tier, response.Usage);
            return response;
        }
    }
}
