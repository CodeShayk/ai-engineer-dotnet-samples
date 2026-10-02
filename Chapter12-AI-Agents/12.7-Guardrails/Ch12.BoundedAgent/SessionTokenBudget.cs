using System.Runtime.CompilerServices;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Ch12.BoundedAgent;

/// <summary>
/// Agent run middleware that tracks token consumption per session and stops gracefully once a
/// budget is exceeded (Chapter 12.7). The table holds sessions weakly, so finished sessions
/// are garbage collected with their counters.
/// </summary>
public sealed class SessionTokenBudget(long maxTokensPerSession)
{
    private readonly ConditionalWeakTable<AgentSession, StrongBox<long>> _used = new();

    public long UsedBy(AgentSession session) => _used.TryGetValue(session, out StrongBox<long>? used) ? used.Value : 0;

    public async Task<AgentResponse> EnforceAsync(
        IEnumerable<ChatMessage> messages, AgentSession? session, AgentRunOptions? options,
        AIAgent innerAgent, CancellationToken cancellationToken)
    {
        StrongBox<long>? used = session is null ? null : _used.GetOrCreateValue(session);

        if (used is not null && used.Value >= maxTokensPerSession)
        {
            return new AgentResponse(new ChatMessage(ChatRole.Assistant,
                "I'm not able to continue this conversation, but a member of our support team will follow up with you."));
        }

        AgentResponse response = await innerAgent.RunAsync(messages, session, options, cancellationToken);

        if (used is not null)
        {
            used.Value += response.Usage?.TotalTokenCount ?? 0;
        }

        return response;
    }
}
