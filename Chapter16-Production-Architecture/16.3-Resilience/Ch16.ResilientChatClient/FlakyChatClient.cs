using System.Net;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace Ch16.Resilience;

/// <summary>How a <see cref="FlakyChatClient"/> misbehaves.</summary>
public enum FailureMode { None, ServerError, RateLimited, Hang }

/// <summary>
/// A fake model deployment that fails on cue, for demonstrating resilience without waiting for
/// a real outage. The first <c>failures</c> calls fail in the chosen way; later calls succeed.
/// </summary>
public sealed class FlakyChatClient(string name, FailureMode mode, int failures = int.MaxValue) : IChatClient
{
    private int _calls;

    public int Calls => _calls;

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        int call = Interlocked.Increment(ref _calls);

        if (call <= failures)
        {
            switch (mode)
            {
                case FailureMode.ServerError:
                    throw new HttpRequestException($"{name}: 503 Service Unavailable", null, HttpStatusCode.ServiceUnavailable);
                case FailureMode.RateLimited:
                    throw new HttpRequestException($"{name}: 429 Too Many Requests", null, HttpStatusCode.TooManyRequests);
                case FailureMode.Hang:
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);   // Until the timeout cancels us
                    break;
            }
        }

        return new ChatResponse(new ChatMessage(ChatRole.Assistant, $"Answer from {name} (call {call})")) { ModelId = name };
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ChatResponse response = await GetResponseAsync(messages, options, cancellationToken);
        foreach (string word in response.Text.Split(' '))
        {
            yield return new ChatResponseUpdate(ChatRole.Assistant, word + " ") { ModelId = name };
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }
}
