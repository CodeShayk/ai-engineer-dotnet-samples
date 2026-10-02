using Microsoft.Agents.AI.Workflows;
using Northwind.Shared.Domain;
using Northwind.Shared.Tools;

namespace Ch10.Workflows;

/// <summary>
/// Validates a refund case with the rules from Chapter 6 and, if it passes, creates a pending
/// request and sends it on for supervisor approval. Invalid cases end the workflow here.
/// </summary>
[SendsMessage(typeof(RefundApprovalRequest))]
[YieldsOutput(typeof(RefundOutcome))]
internal sealed partial class ValidateRefundExecutor(NorthwindStore store, ReturnPolicyRules returnRules) : Executor("ValidateRefund")
{
    [MessageHandler]
    private async ValueTask HandleAsync(RefundCase refundCase, IWorkflowContext context)
    {
        // The customer comes from the case, which a real system takes from the authenticated user.
        var tools = new RefundTools(store, returnRules, new FixedCurrentCustomer(refundCase.CustomerId));
        RefundRequestResult result = tools.RequestRefund(refundCase.OrderNumber, refundCase.ProductId, refundCase.Reason);

        if (!result.Accepted)
        {
            await context.YieldOutputAsync(new RefundOutcome(refundCase.OrderNumber, "Rejected", result.Message));
            return;
        }

        await context.SendMessageAsync(new RefundApprovalRequest(
            result.RequestId!, refundCase.OrderNumber, refundCase.ProductId, result.Amount!.Value, refundCase.Reason.ToString()));
    }
}

/// <summary>Records the supervisor's decision and reports the outcome.</summary>
[YieldsOutput(typeof(RefundOutcome))]
internal sealed partial class ExecuteRefundExecutor(NorthwindStore store) : Executor("ExecuteRefund")
{
    [MessageHandler]
    private async ValueTask HandleAsync(RefundDecision decision, IWorkflowContext context)
    {
        RefundRequest? request = store.Decide(decision.RequestId, decision.Approved);
        if (request is null)
        {
            await context.YieldOutputAsync(new RefundOutcome("?", "Error", $"Refund request {decision.RequestId} was not found."));
            return;
        }

        string message = decision.Approved
            ? $"Refund {request.RequestId} of {request.Amount:0.00} approved. The customer will be refunded to the original payment method."
            : $"Refund {request.RequestId} declined: {decision.Note ?? "no reason given"}.";

        await context.YieldOutputAsync(new RefundOutcome(request.OrderId, request.Status.ToString(), message));
    }
}
