using System.Security.Claims;
using Ch16.NorthwindAssist.Api.Orchestration;
using Ch16.NorthwindAssist.Api.Platform;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Ch16.NorthwindAssist.Api.Endpoints;

public sealed record ApprovalDecision(bool Approved, string? Reason);

/// <summary>
/// The supervisor approval queue (Chapters 6.4 and 10.5). A paused conversation waits in the
/// session store, possibly for hours; a decision restores the session and resumes the run on
/// behalf of the conversation's owner.
/// </summary>
public static class ApprovalEndpoints
{
    public static IEndpointRouteBuilder MapApprovalEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/approvals").RequireAuthorization(PlatformExtensions.SupervisorPolicy);

        group.MapGet("/", ListAsync);
        group.MapPost("/{conversationId}/{approvalId}", DecideAsync);

        return app;
    }

    private static async Task<IResult> ListAsync(ISessionStore sessions, CancellationToken cancellationToken)
    {
        IReadOnlyList<ConversationRecord> waiting = await sessions.ListAwaitingApprovalAsync(cancellationToken);

        return Results.Ok(waiting.SelectMany(record => record.PendingApprovals.Select(p => new
        {
            record.ConversationId,
            Customer = record.OwnerCustomerId,
            p.ApprovalId,
            p.ToolName,
            p.Arguments,
            p.RequestedAt
        })));
    }

    private static async Task<IResult> DecideAsync(
        string conversationId,
        string approvalId,
        ApprovalDecision decision,
        ClaimsPrincipal supervisor,
        RequestCustomer requestCustomer,
        NorthwindAssistFactory factory,
        ISessionStore sessions,
        ConversationRunner runner,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        ILogger logger = loggerFactory.CreateLogger("Northwind.Approvals");

        ConversationRecord? record = await sessions.LoadAsync(conversationId, cancellationToken);
        PendingApproval? pending = record?.PendingApprovals.FirstOrDefault(p => p.ApprovalId == approvalId);
        if (record is null || pending is null || record.AgentSessionState is not { } state)
        {
            return Results.NotFound();
        }

        // Resume on behalf of the customer who owns the conversation, and record who decided.
        requestCustomer.ActFor(record.OwnerCustomerId);
        logger.LogInformation("Supervisor {Supervisor} {Decision} {Tool} ({ApprovalId}) for customer {Customer}",
            supervisor.Identity?.Name, decision.Approved ? "approved" : "declined", pending.ToolName, approvalId, record.OwnerCustomerId);

        AIAgent agent = await factory.CreateAsync(record.Variant, cancellationToken);
        AgentSession session = await agent.DeserializeSessionAsync(state, cancellationToken: cancellationToken);

        ToolApprovalRequestContent request = ConversationRunner.RestoreApproval(pending);
        AgentResponse response = await agent.RunAsync(
            new ChatMessage(ChatRole.User, [request.CreateResponse(decision.Approved, decision.Approved ? null : decision.Reason ?? "Declined by a supervisor.")]),
            session,
            cancellationToken: cancellationToken);

        record.PendingApprovals.RemoveAll(p => p.ApprovalId == approvalId);
        record.Transcript.Add(new TranscriptEntry("system",
            $"A supervisor {(decision.Approved ? "approved" : "declined")} the {pending.ToolName} request.", DateTimeOffset.UtcNow));

        string reply = runner.MakeSafe(response.Text, conversationId);
        if (reply.Length > 0)
        {
            record.Transcript.Add(new TranscriptEntry("assistant", reply, DateTimeOffset.UtcNow));
        }

        ConversationRunner.NewApprovals(response.Messages.SelectMany(m => m.Contents), record);
        await ConversationRunner.SaveSessionAsync(agent, session, record, sessions, cancellationToken);

        return Results.Ok(new { conversationId, approvalId, decision.Approved, reply });
    }
}
