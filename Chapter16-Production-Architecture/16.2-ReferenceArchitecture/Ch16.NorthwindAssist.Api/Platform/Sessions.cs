using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;

namespace Ch16.NorthwindAssist.Api.Platform;

/// <summary>One line of the conversation as shown to people: the customer, the assistant or a system note.</summary>
public sealed record TranscriptEntry(string Role, string Text, DateTimeOffset At);

/// <summary>A refund (or other) action waiting for a supervisor, with the serialized approval request.</summary>
public sealed record PendingApproval(string ApprovalId, string ToolName, string Arguments, string RequestJson, DateTimeOffset RequestedAt);

/// <summary>
/// Everything stored for one conversation: its owner, the agent session state, the transcript
/// and any pending approvals. Binding the record to its owner enforces the rule from Chapter 10.4:
/// a session is only ever resumed for the customer who started it.
/// </summary>
public sealed record ConversationRecord(
    string ConversationId,
    string OwnerCustomerId,
    string Variant,
    JsonElement? AgentSessionState,
    List<TranscriptEntry> Transcript,
    List<PendingApproval> PendingApprovals);

public interface ISessionStore
{
    Task<ConversationRecord?> LoadAsync(string conversationId, CancellationToken cancellationToken);

    Task SaveAsync(ConversationRecord record, CancellationToken cancellationToken);

    /// <summary>Every conversation with a pending approval, for the supervisor queue.</summary>
    Task<IReadOnlyList<ConversationRecord>> ListAwaitingApprovalAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Stores conversations in IDistributedCache as JSON, with a sliding expiry. AddDistributedMemoryCache
/// keeps this in memory for local runs; swapping in Redis makes it shared across instances.
/// </summary>
public sealed class DistributedCacheSessionStore(IDistributedCache cache) : ISessionStore
{
    private const string IndexKey = "conversations:awaiting-approval";
    private static readonly DistributedCacheEntryOptions Expiry = new() { SlidingExpiration = TimeSpan.FromDays(7) };
    private static readonly SemaphoreSlim IndexGate = new(1, 1);

    public async Task<ConversationRecord?> LoadAsync(string conversationId, CancellationToken cancellationToken)
    {
        byte[]? json = await cache.GetAsync(Key(conversationId), cancellationToken);
        return json is null ? null : JsonSerializer.Deserialize<ConversationRecord>(json, AIJsonUtilities.DefaultOptions);
    }

    public async Task SaveAsync(ConversationRecord record, CancellationToken cancellationToken)
    {
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(record, AIJsonUtilities.DefaultOptions);
        await cache.SetAsync(Key(record.ConversationId), json, Expiry, cancellationToken);
        await UpdateIndexAsync(record.ConversationId, awaiting: record.PendingApprovals.Count > 0, cancellationToken);
    }

    public async Task<IReadOnlyList<ConversationRecord>> ListAwaitingApprovalAsync(CancellationToken cancellationToken)
    {
        var results = new List<ConversationRecord>();
        foreach (string id in await ReadIndexAsync(cancellationToken))
        {
            if (await LoadAsync(id, cancellationToken) is { PendingApprovals.Count: > 0 } record)
            {
                results.Add(record);
            }
        }

        return results;
    }

    private async Task UpdateIndexAsync(string conversationId, bool awaiting, CancellationToken cancellationToken)
    {
        await IndexGate.WaitAsync(cancellationToken);
        try
        {
            HashSet<string> index = await ReadIndexAsync(cancellationToken);
            bool changed = awaiting ? index.Add(conversationId) : index.Remove(conversationId);
            if (changed)
            {
                await cache.SetAsync(IndexKey, JsonSerializer.SerializeToUtf8Bytes(index), Expiry, cancellationToken);
            }
        }
        finally
        {
            IndexGate.Release();
        }
    }

    private async Task<HashSet<string>> ReadIndexAsync(CancellationToken cancellationToken)
    {
        byte[]? json = await cache.GetAsync(IndexKey, cancellationToken);
        return json is null ? [] : JsonSerializer.Deserialize<HashSet<string>>(json) ?? [];
    }

    private static string Key(string conversationId) => $"conversation:{conversationId}";
}
