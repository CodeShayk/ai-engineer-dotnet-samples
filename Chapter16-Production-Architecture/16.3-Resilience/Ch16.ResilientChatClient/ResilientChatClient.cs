using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using Polly;

namespace Ch16.Resilience;

/// <summary>Applies a resilience pipeline to every call to a chat client (Chapter 16.3).</summary>
public sealed class ResilientChatClient(IChatClient innerClient, ResiliencePipeline pipeline)
    : DelegatingChatClient(innerClient)
{
    public override Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        List<ChatMessage> request = [.. messages];   // Materialize once; the request may be sent more than once.

        return pipeline.ExecuteAsync(
            async token => await base.GetResponseAsync(request, options, token),
            cancellationToken).AsTask();
    }

    /// <summary>
    /// For streaming, the pipeline covers the call up to the first update. Once text has reached
    /// the user, a retry would repeat it, so later failures are passed to the caller.
    /// </summary>
    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        List<ChatMessage> request = [.. messages];

        (IAsyncEnumerator<ChatResponseUpdate> enumerator, bool hasFirst) = await pipeline.ExecuteAsync(async token =>
        {
            IAsyncEnumerator<ChatResponseUpdate> e = base.GetStreamingResponseAsync(request, options, cancellationToken)
                .GetAsyncEnumerator(cancellationToken);
            try
            {
                return (e, await e.MoveNextAsync());
            }
            catch
            {
                await e.DisposeAsync();
                throw;
            }
        }, cancellationToken);

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
    }
}
