using System.ComponentModel;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Northwind.Shared.Domain;

namespace Northwind.Shared.Tools;

[JsonConverter(typeof(JsonStringEnumConverter<RefundReason>))]
public enum RefundReason
{
    ChangedMind,
    WrongItem,
    DoesNotFit,
    DamagedOrFaulty,
    Other
}

/// <summary>The result of a refund request, written for the model to relay to the customer.</summary>
public sealed record RefundRequestResult(bool Accepted, string? RequestId, decimal? Amount, string Message)
{
    public static RefundRequestResult Rejected(string message) => new(false, null, null, message);

    public static RefundRequestResult Pending(string requestId, decimal amount) =>
        new(true, requestId, amount,
            $"Refund request {requestId} for {amount:0.00} has been created and is awaiting supervisor approval.");
}

/// <summary>
/// The refund tool from Chapter 6.5. The model can ask for a refund; it cannot choose the
/// amount, act on another customer's order, bypass the returns policy or create duplicates.
/// </summary>
public sealed class RefundTools(
    NorthwindStore store,
    ReturnPolicyRules returnRules,
    ICurrentCustomer currentCustomer,
    ILogger<RefundTools>? logger = null)
{
    private readonly ILogger _logger = logger ?? NullLogger<RefundTools>.Instance;

    [Description("Creates a refund request for one item in one of the signed-in customer's orders. " +
                 "Only use this after CheckReturnEligibility has confirmed the item is eligible. " +
                 "The request must be approved by a support agent before any money is refunded.")]
    public RefundRequestResult RequestRefund(
        [Description("The order number, in the format NW-#####.")] string orderNumber,
        [Description("The product ID of the item to refund, for example P05.")] string productId,
        [Description("The customer's reason for the refund.")] RefundReason reason)
    {
        Order? order = store.FindOrder(orderNumber.Trim().ToUpperInvariant());

        // Authorization: the order must belong to the authenticated customer.
        if (order is null || order.CustomerId != currentCustomer.CustomerId)
        {
            return RefundRequestResult.Rejected("No order with that number was found on this account.");
        }

        OrderLine? line = order.Lines.FirstOrDefault(l => l.ProductId.Equals(productId, StringComparison.OrdinalIgnoreCase));
        if (line is null)
        {
            return RefundRequestResult.Rejected($"Order {order.OrderId} does not contain product {productId}.");
        }

        // Business rules are enforced in code, whatever the model believes. Damaged or faulty
        // items are refundable outside the usual rules, but only once they have been delivered.
        ReturnEligibility eligibility = returnRules.Evaluate(order, productId, opened: true, DateOnly.FromDateTime(DateTime.Today));
        bool damagedAfterDelivery = reason == RefundReason.DamagedOrFaulty && order.Status == OrderStatus.Delivered;
        if (!eligibility.Eligible && !damagedAfterDelivery)
        {
            return RefundRequestResult.Rejected(eligibility.Reason);
        }

        // Idempotent: one request per order line. The amount comes from our data, not the model.
        RefundRequest request = store.GetOrCreateRefundRequest(order.OrderId, productId, line.LineTotal, reason.ToString());

        _logger.LogInformation("Refund request {RequestId} for {OrderId}/{ProductId} raised by assistant for customer {CustomerId}",
            request.RequestId, order.OrderId, productId, currentCustomer.CustomerId);

        return RefundRequestResult.Pending(request.RequestId, request.Amount);
    }
}
