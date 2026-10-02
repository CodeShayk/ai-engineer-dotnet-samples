using Ch05.TypedResponses;
using Northwind.Shared.Tools;

namespace Ch10.Workflows;

// --- The support triage workflow ----------------------------------------------------------------

public sealed record SupportRequest(string CustomerId, string Message);

public sealed record TriagedRequest(SupportRequest Request, TicketTriage Triage);

public sealed record SupportOutcome(string Route, string Reply);

// --- The refund workflow ---------------------------------------------------------------------------

public sealed record RefundCase(string CustomerId, string OrderNumber, string ProductId, RefundReason Reason);

public sealed record RefundApprovalRequest(string RequestId, string OrderNumber, string ProductId, decimal Amount, string Reason);

public sealed record RefundDecision(string RequestId, bool Approved, string? Note);

public sealed record RefundOutcome(string OrderNumber, string Status, string Message);
