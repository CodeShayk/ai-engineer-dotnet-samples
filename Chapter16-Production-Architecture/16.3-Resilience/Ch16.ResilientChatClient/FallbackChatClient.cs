using System.ClientModel;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace Ch16.Resilience;

/// <summary>
/// Tries a list of chat clients in order, moving on when a call fails with a recoverable error
/// or the circuit is open (Chapter 16.3). Each client should have its own resilience pipeline,
/// and every fallback target should be independent and evaluated.
/// </summary>
public sealed class FallbackChatClient(IReadOnlyList<IChatClient> clients, ILogger<FallbackChatClient> logger) : IChatClient
{
    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        List<ChatMessage> request = [.. messages];

        for (int i = 0; ; i++)
        {
            try
            {
                return await clients[i].GetResponseAsync(request, options, cancellationToken);
            }
            catch (Exception ex) when (i < clients.Count - 1 && IsRecoverable(ex) && !cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Model client {Index} failed; falling back to client {Next}", i, i + 1);
            }
        }
    }

    // The streaming implementation falls back only if the failure occurs before the first update.
    // Once the user has seen part of an answer, switching models mid-sentence would be worse than failing.
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        List<ChatMessage> request = [.. messages];

        for (int i = 0; ; i++)
        {
            IAsyncEnumerator<ChatResponseUpdate> enumerator = clients[i]
                .GetStreamingResponseAsync(request, options, cancellationToken)
                .GetAsyncEnumerator(cancellationToken);

            bool hasFirst;
            try
            {
                hasFirst = await enumerator.MoveNextAsync();
            }
            catch (Exception ex) when (i < clients.Count - 1 && IsRecoverable(ex) && !cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Model client {Index} failed before streaming began; falling back to client {Next}", i, i + 1);
                await enumerator.DisposeAsync();
                continue;
            }

            await using (enumerator)
            {
                if (!hasFirst)
                {
                    yield break;
                }

                yield return enumerator.Current;

                while (await enumerator.MoveNextAsync())
                {
                    yield return enumerator.Current;
                }
            }

            yield break;
        }
    }

    private static bool IsRecoverable(Exception ex) =>
        ex is BrokenCircuitException or TimeoutRejectedException or HttpRequestException
        || ex is ClientResultException { Status: 429 or >= 500 };

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceType.IsInstanceOfType(this) ? this : clients[0].GetService(serviceType, serviceKey);

    public void Dispose()
    {
        foreach (IChatClient client in clients)
        {
            client.Dispose();
        }
    }
}
