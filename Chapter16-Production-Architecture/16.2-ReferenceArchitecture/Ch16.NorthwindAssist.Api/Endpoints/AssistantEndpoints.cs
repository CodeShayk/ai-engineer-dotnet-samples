using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Ch16.NorthwindAssist.Api.Orchestration;
using Ch16.NorthwindAssist.Api.Platform;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Northwind.Shared.Domain;
using Northwind.Shared.Security;

namespace Ch16.NorthwindAssist.Api.Endpoints;

public sealed record AssistantChatRequest(string? ConversationId, string Message);

/// <summary>
/// The chat endpoints. Replies stream as Server-Sent Events:
///   delta     raw text as it is generated; render it as plain text only
///   final     the complete reply as safe HTML, after leak scanning and sanitization; replace the streamed text with it
///   approval  an action is waiting for a supervisor
///   done      the conversation id, the variant (stable or canary) and the agent that answered
/// </summary>
public static class AssistantEndpoints
{
    private const int MaxMessageLength = 2_000;

    public static IEndpointRouteBuilder MapAssistantEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/assistant").RequireAuthorization();

        group.MapPost("/chat", Chat).RequireRateLimiting(PlatformExtensions.ChatRateLimitPolicy);
        group.MapGet("/conversations/{conversationId}", GetConversationAsync);

        return app;
    }

    private static IResult Chat(
        AssistantChatRequest request,
        HttpContext httpContext,
        ICurrentCustomer currentCustomer,
        NorthwindAssistFactory factory,
        ISessionStore sessions,
        IPromptAttackDetector attackDetector,
        ConversationRunner runner,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message) || request.Message.Length > MaxMessageLength)
        {
            return Results.BadRequest($"Messages must be between 1 and {MaxMessageLength} characters.");
        }

        string conversationId = string.IsNullOrWhiteSpace(request.ConversationId) ? Guid.NewGuid().ToString("N") : request.ConversationId;
        string variant = factory.VariantFor(conversationId);
        httpContext.Response.Headers["X-Northwind-Variant"] = variant;

        return Results.ServerSentEvents(RunAsync(
            request.Message, conversationId, variant, currentCustomer.CustomerId, factory, sessions, attackDetector, runner, cancellationToken));
    }

    private static async IAsyncEnumerable<SseItem<string>> RunAsync(
        string message,
        string conversationId,
        string variant,
        string customerId,
        NorthwindAssistFactory factory,
        ISessionStore sessions,
        IPromptAttackDetector attackDetector,
        ConversationRunner runner,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ConversationRecord? existing = await sessions.LoadAsync(conversationId, cancellationToken);

        // Sessions are bound to their owner. Another customer's conversation is reported as not found.
        if (existing is not null && existing.OwnerCustomerId != customerId)
        {
            yield return new SseItem<string>("Conversation not found.", "error");
            yield break;
        }

        ConversationRecord record = existing ?? new ConversationRecord(conversationId, customerId, variant, null, [], []);
        record.Transcript.Add(new TranscriptEntry("customer", message, DateTimeOffset.UtcNow));

        if (record.PendingApprovals.Count > 0)
        {
            const string waiting = "Your request is with a supervisor for approval. I'll update this conversation as soon as they decide.";
            record.Transcript.Add(new TranscriptEntry("assistant", waiting, DateTimeOffset.UtcNow));
            await sessions.SaveAsync(record, cancellationToken);
            yield return new SseItem<string>(waiting, "final");
            yield return Done(conversationId, record.Variant);
            yield break;
        }

        // Input screening: a direct attack gets a polite refusal and never reaches the agents.
        PromptAttackResult screening = await attackDetector.AnalyzeAsync(message, [], cancellationToken);
        if (screening.UserPromptAttack)
        {
            const string refusal = "I can't help with that request, but I'm happy to help with your orders, deliveries and returns.";
            record.Transcript.Add(new TranscriptEntry("assistant", refusal, DateTimeOffset.UtcNow));
            await sessions.SaveAsync(record, cancellationToken);
            yield return new SseItem<string>(refusal, "final");
            yield return Done(conversationId, record.Variant);
            yield break;
        }

        AIAgent agent = await factory.CreateAsync(record.Variant, cancellationToken);
        AgentSession session = record.AgentSessionState is { } state
            ? await agent.DeserializeSessionAsync(state, cancellationToken: cancellationToken)
            : await agent.CreateSessionAsync(cancellationToken);

        var updates = new List<AgentResponseUpdate>();
        var text = new StringBuilder();
        string? answeredBy = null;

        await foreach (AgentResponseUpdate update in agent.RunStreamingAsync(message, session, cancellationToken: cancellationToken))
        {
            updates.Add(update);
            if (!string.IsNullOrEmpty(update.Text))
            {
                answeredBy = update.AuthorName ?? answeredBy;
                text.Append(update.Text);
                yield return new SseItem<string>(update.Text, "delta");
            }
        }

        string reply = runner.MakeSafe(text.ToString(), conversationId);
        record.Transcript.Add(new TranscriptEntry("assistant", reply, DateTimeOffset.UtcNow));
        yield return new SseItem<string>(reply, "final");

        foreach (PendingApproval approval in ConversationRunner.NewApprovals(updates.SelectMany(u => u.Contents), record))
        {
            record.Transcript.Add(new TranscriptEntry("system", $"Waiting for supervisor approval: {approval.ToolName}", DateTimeOffset.UtcNow));
            yield return new SseItem<string>(JsonSerializer.Serialize(new { approval.ApprovalId, approval.ToolName, approval.Arguments }), "approval");
        }

        await ConversationRunner.SaveSessionAsync(agent, session, record, sessions, cancellationToken);
        yield return Done(conversationId, record.Variant, answeredBy);
    }

    private static async Task<IResult> GetConversationAsync(
        string conversationId, ICurrentCustomer currentCustomer, ISessionStore sessions, CancellationToken cancellationToken)
    {
        ConversationRecord? record = await sessions.LoadAsync(conversationId, cancellationToken);

        return record is null || record.OwnerCustomerId != currentCustomer.CustomerId
            ? Results.NotFound()
            : Results.Ok(new
            {
                record.ConversationId,
                record.Variant,
                record.Transcript,
                PendingApprovals = record.PendingApprovals.Select(p => new { p.ApprovalId, p.ToolName, p.RequestedAt })
            });
    }

    private static SseItem<string> Done(string conversationId, string variant, string? agent = null) =>
        new(JsonSerializer.Serialize(new { conversationId, variant, agent }), "done");
}
