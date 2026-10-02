using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace Northwind.Shared.Testing;

/// <summary>
/// A fake <see cref="IChatClient"/> that returns canned replies and records every request,
/// so code around the model can be tested without calling one (Chapter 4.7).
/// </summary>
public sealed class FakeChatClient(params string[] replies) : IChatClient
{
    private int _next;

    public List<List<ChatMessage>> Requests { get; } = [];

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        Requests.Add(messages.ToList());
        string reply = replies.Length == 0 ? "" : replies[Math.Min(_next++, replies.Length - 1)];
        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, reply)));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ChatResponse response = await GetResponseAsync(messages, options, cancellationToken);
        foreach (ChatResponseUpdate update in response.ToChatResponseUpdates())
        {
            yield return update;
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
