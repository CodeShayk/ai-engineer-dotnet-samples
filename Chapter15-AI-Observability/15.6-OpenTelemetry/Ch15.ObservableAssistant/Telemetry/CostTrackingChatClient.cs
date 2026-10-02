using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace Ch15.ObservableAssistant.Telemetry;

/// <summary>Per-million-token prices for one model, in US dollars.</summary>
public sealed record ModelPrice(double InputPerMillion, double CachedInputPerMillion, double OutputPerMillion);

/// <summary>
/// Prices by model, loaded from configuration because they change. A model id such as
/// "gpt-5-mini-2025-08-07" matches the longest configured name it starts with ("gpt-5-mini").
/// </summary>
public sealed class ModelPriceList(IReadOnlyDictionary<string, ModelPrice> prices)
{
    public bool TryGetPrice(string modelId, out ModelPrice price)
    {
        if (prices.TryGetValue(modelId, out price!))
        {
            return true;
        }

        string? key = prices.Keys
            .Where(k => modelId.StartsWith(k, StringComparison.OrdinalIgnoreCase))
            .MaxBy(k => k.Length);

        price = key is null ? default! : prices[key];
        return key is not null;
    }
}

/// <summary>
/// Converts token usage into an estimated cost and records it as a metric, tagged with the model
/// and the feature that made the call (Chapter 15.4). Estimates, not invoices, but they arrive
/// in seconds and break down by feature.
/// </summary>
public sealed class CostTrackingChatClient(IChatClient innerClient, ModelPriceList prices, Meter meter)
    : DelegatingChatClient(innerClient)
{
    private readonly Counter<double> _estimatedCost = meter.CreateCounter<double>(
        "northwind.ai.estimated_cost", unit: "USD", description: "Estimated model spend based on token usage");

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        ChatResponse response = await base.GetResponseAsync(messages, options, cancellationToken);
        Record(response.ModelId, response.Usage);
        return response;
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
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

        Record(modelId, usage);
    }

    private void Record(string? modelId, UsageDetails? usage)
    {
        if (usage is null || modelId is null || !prices.TryGetPrice(modelId, out ModelPrice price))
        {
            return;
        }

        long cachedInput = usage.CachedInputTokenCount ?? 0;
        long uncachedInput = (usage.InputTokenCount ?? 0) - cachedInput;

        double cost = uncachedInput * price.InputPerMillion / 1_000_000
                    + cachedInput * price.CachedInputPerMillion / 1_000_000
                    + (usage.OutputTokenCount ?? 0) * price.OutputPerMillion / 1_000_000;

        _estimatedCost.Add(cost,
            new KeyValuePair<string, object?>("gen_ai.response.model", modelId),
            new KeyValuePair<string, object?>("northwind.feature", AIFeature.Current ?? "unknown"));
    }
}
