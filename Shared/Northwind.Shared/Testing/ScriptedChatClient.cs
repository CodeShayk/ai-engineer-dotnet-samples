using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace Northwind.Shared.Testing;

/// <summary>
/// A fake <see cref="IChatClient"/> that plays a fixed script: each call returns the next step's
/// message. Scripts can request tool calls, so tests can simulate a model that has been
/// manipulated into doing something it should not, and check that the code around it holds.
/// </summary>
public sealed class ScriptedChatClient(params Func<IReadOnlyList<ChatMessage>, ChatMessage>[] steps) : IChatClient
{
    private int _next;

    /// <summary>Every request the client received, in order.</summary>
    public List<List<ChatMessage>> Requests { get; } = [];

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        List<ChatMessage> request = messages.ToList();
        Requests.Add(request);

        Func<IReadOnlyList<ChatMessage>, ChatMessage> step = steps[Math.Min(_next++, steps.Length - 1)];
        return Task.FromResult(new ChatResponse(step(request)) { ModelId = "scripted" });
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

    public void Dispose() { }

    /// <summary>A step that requests one tool call.</summary>
    public static Func<IReadOnlyList<ChatMessage>, ChatMessage> Call(string toolName, params (string Name, object? Value)[] arguments) =>
        _ => new ChatMessage(ChatRole.Assistant, [CallContent(toolName, arguments)]);

    /// <summary>A step that requests several tool calls in a single response.</summary>
    public static Func<IReadOnlyList<ChatMessage>, ChatMessage> CallMany(string toolName, int count, params (string Name, object? Value)[] arguments) =>
        _ => new ChatMessage(ChatRole.Assistant, Enumerable.Range(0, count).Select(_ => (AIContent)CallContent(toolName, arguments)).ToList());

    /// <summary>A step that replies with text.</summary>
    public static Func<IReadOnlyList<ChatMessage>, ChatMessage> Say(string text) =>
        _ => new ChatMessage(ChatRole.Assistant, text);

    private static FunctionCallContent CallContent(string toolName, (string Name, object? Value)[] arguments) =>
        new($"call_{Guid.NewGuid():N}"[..13], toolName, arguments.ToDictionary(a => a.Name, a => a.Value));
}
