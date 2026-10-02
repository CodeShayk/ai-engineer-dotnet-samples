using Ch03.FewShotClassification;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Ch04.ClientPipeline;

/// <summary>Routes incoming tickets using the keyed "small" chat client (Chapter 4.6).</summary>
public sealed class TicketTriageService([FromKeyedServices("small")] IChatClient chatClient)
{
    // Classification is a simple task: the small model is faster and much cheaper.
    private readonly TicketClassifier _classifier = new(chatClient);

    public Task<TicketCategory> TriageAsync(string message, CancellationToken cancellationToken = default) =>
        _classifier.ClassifyAsync(message, cancellationToken);
}
