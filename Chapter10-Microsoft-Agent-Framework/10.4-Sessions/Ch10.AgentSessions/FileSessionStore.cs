using System.Text.Json;
using Microsoft.Agents.AI;

namespace Ch10.AgentSessions;

/// <summary>
/// Saves agent sessions as JSON files and restores them, so a conversation survives a
/// restart (Chapter 10.4). In production, the same pattern stores session state in a
/// database or distributed cache, together with the identity of the user who owns it.
/// </summary>
public sealed class FileSessionStore(string directory)
{
    public async Task SaveAsync(AIAgent agent, string conversationId, AgentSession session, CancellationToken cancellationToken)
    {
        JsonElement state = await agent.SerializeSessionAsync(session, cancellationToken: cancellationToken);
        await File.WriteAllTextAsync(PathFor(conversationId), state.GetRawText(), cancellationToken);
    }

    public async Task<AgentSession> LoadOrCreateAsync(AIAgent agent, string conversationId, CancellationToken cancellationToken)
    {
        string path = PathFor(conversationId);
        if (!File.Exists(path))
        {
            return await agent.CreateSessionAsync(cancellationToken);
        }

        using JsonDocument document = JsonDocument.Parse(await File.ReadAllTextAsync(path, cancellationToken));
        return await agent.DeserializeSessionAsync(document.RootElement.Clone(), cancellationToken: cancellationToken);
    }

    public bool Exists(string conversationId) => File.Exists(PathFor(conversationId));

    public void Delete(string conversationId) => File.Delete(PathFor(conversationId));

    private string PathFor(string conversationId) => Path.Combine(directory, $"{conversationId}.json");
}
