using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Ch07.HybridRouting;

/// <summary>
/// Sends requests that contain personal or payment data to a local model, and everything
/// else to the cloud model (Chapter 7.5). Callers see a single IChatClient.
/// </summary>
public sealed class SensitiveDataRoutingChatClient(
    IChatClient cloudClient,
    IChatClient localClient,
    ISensitiveDataDetector detector,
    ILogger<SensitiveDataRoutingChatClient> logger) : IChatClient
{
    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        Select(messages).GetResponseAsync(messages, options, cancellationToken);

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        Select(messages).GetStreamingResponseAsync(messages, options, cancellationToken);

    private IChatClient Select(IEnumerable<ChatMessage> messages)
    {
        bool sensitive = messages.Any(m => detector.ContainsSensitiveData(m.Text));
        logger.LogInformation("Routing request to the {Target} model", sensitive ? "local" : "cloud");
        return sensitive ? localClient : cloudClient;
    }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceType.IsInstanceOfType(this) ? this : cloudClient.GetService(serviceType, serviceKey);

    public void Dispose()
    {
        cloudClient.Dispose();
        localClient.Dispose();
    }
}
