using System.Text.Json;
using Ch16.NorthwindAssist.Api.Platform;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Northwind.Shared.Security;

namespace Ch16.NorthwindAssist.Api.Endpoints;

/// <summary>Helpers shared by the chat and approval endpoints: output safety and approval bookkeeping.</summary>
public sealed class ConversationRunner(AssistantOutputSanitizer sanitizer, OutputLeakScanner leakScanner, ILogger<ConversationRunner> logger)
{
    /// <summary>The last line of defense before text reaches a browser: leak scanning, then sanitization.</summary>
    public string MakeSafe(string text, string conversationId)
    {
        OutputScanResult scan = leakScanner.Scan(text);
        if (scan.LeakDetected)
        {
            logger.LogWarning("Blocked {Findings} in a reply in conversation {ConversationId}", string.Join(", ", scan.Findings), conversationId);
        }

        return sanitizer.Sanitize(scan.SafeText);
    }

    /// <summary>Records each new approval request once; a response can report the same request more than once.</summary>
    public static List<PendingApproval> NewApprovals(IEnumerable<AIContent> contents, ConversationRecord record)
    {
        var added = new List<PendingApproval>();

        foreach (ToolApprovalRequestContent request in contents.OfType<ToolApprovalRequestContent>().DistinctBy(r => r.RequestId))
        {
            if (record.PendingApprovals.Any(p => p.ApprovalId == request.RequestId))
            {
                continue;
            }

            var call = request.ToolCall as FunctionCallContent;
            var pending = new PendingApproval(
                request.RequestId,
                call?.Name ?? "unknown",
                JsonSerializer.Serialize(call?.Arguments ?? new Dictionary<string, object?>()),
                JsonSerializer.Serialize<AIContent>(request, AIJsonUtilities.DefaultOptions),
                DateTimeOffset.UtcNow);

            record.PendingApprovals.Add(pending);
            added.Add(pending);
        }

        return added;
    }

    public static ToolApprovalRequestContent RestoreApproval(PendingApproval pending) =>
        (ToolApprovalRequestContent)JsonSerializer.Deserialize<AIContent>(pending.RequestJson, AIJsonUtilities.DefaultOptions)!;

    public static async Task SaveSessionAsync(AIAgent agent, AgentSession session, ConversationRecord record, ISessionStore store, CancellationToken cancellationToken)
    {
        JsonElement state = await agent.SerializeSessionAsync(session, cancellationToken: cancellationToken);
        await store.SaveAsync(record with { AgentSessionState = state }, cancellationToken);
    }
}
