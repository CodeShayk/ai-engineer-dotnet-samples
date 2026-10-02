using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Ch06.ManualInvocation;

/// <summary>
/// A stand-in for a model that follows a fixed script: each call returns the next step's
/// message. It lets you run the tool-calling mechanics offline with --offline, and see
/// exactly which messages flow between the application and the "model".
/// </summary>
public sealed class ScriptedChatClient(params Func<IReadOnlyList<ChatMessage>, ChatMessage>[] steps) : IChatClient
{
    private int _next;

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        Func<IReadOnlyList<ChatMessage>, ChatMessage> step = steps[Math.Min(_next++, steps.Length - 1)];
        return Task.FromResult(new ChatResponse(step(messages.ToList())) { ModelId = "scripted" });
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
        _ => new ChatMessage(ChatRole.Assistant,
        [
            new FunctionCallContent($"call_{Guid.NewGuid():N}"[..13], toolName, arguments.ToDictionary(a => a.Name, a => a.Value))
        ]);

    /// <summary>A final step that "answers" by reporting the most recent tool result.</summary>
    public static ChatMessage SummarizeLastResult(IReadOnlyList<ChatMessage> history)
    {
        FunctionResultContent? last = history.SelectMany(m => m.Contents).OfType<FunctionResultContent>().LastOrDefault();
        return new ChatMessage(ChatRole.Assistant,
            $"(Scripted answer) The last tool result was: {(last is null ? "none" : JsonSerializer.Serialize(last.Result))}");
    }
}
