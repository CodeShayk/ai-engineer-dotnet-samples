using Microsoft.Extensions.AI;

namespace Ch04.StreamingApi;

public sealed record ChatTurn(string Role, string Content);

public sealed record ChatRequest(IReadOnlyList<ChatTurn> Messages)
{
    // Only user and assistant turns are accepted from the client. The system prompt
    // is always supplied by the server and can never be overridden by a caller.
    public IEnumerable<ChatMessage> ToChatMessages() =>
        Messages
            .Where(m => m.Role is "user" or "assistant")
            .Select(m => new ChatMessage(m.Role == "user" ? ChatRole.User : ChatRole.Assistant, m.Content));
}
