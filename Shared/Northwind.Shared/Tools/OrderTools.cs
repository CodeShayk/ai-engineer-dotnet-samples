using System.ComponentModel;
using Northwind.Shared.Domain;

namespace Northwind.Shared.Tools;

/// <summary>
/// Read-only order tools (Chapter 6.2). Thin, well-described wrappers around the domain
/// services; the business rules live in <see cref="ReturnPolicyRules"/>, not in the model.
/// </summary>
public sealed class OrderTools(NorthwindStore store, ReturnPolicyRules returnRules)
{
    [Description("Gets the status, items and delivery details of a Northwind order. " +
                 "Use this whenever the customer asks about a specific order.")]
    public OrderLookupResult GetOrderStatus(
        [Description("The order number, in the format NW-##### (for example NW-10249).")] string orderNumber)
    {
        Order? order = store.FindOrder(orderNumber.Trim().ToUpperInvariant());

        return order is null
            ? OrderLookupResult.NotFound(orderNumber)
            : OrderLookupResult.FromOrder(order);
    }

    [Description("Checks whether an item from an order can be returned under Northwind's returns policy, " +
                 "and if so, the last date it can be returned.")]
    public ReturnEligibility CheckReturnEligibility(
        [Description("The order number, in the format NW-#####.")] string orderNumber,
        [Description("The product ID of the item, for example P01. Get this from GetOrderStatus.")] string productId,
        [Description("True if the customer has opened or used the item.")] bool opened)
    {
        Order? order = store.FindOrder(orderNumber.Trim().ToUpperInvariant());

        return order is null
            ? ReturnEligibility.NotEligible($"Order {orderNumber} was not found.")
            : returnRules.Evaluate(order, productId, opened, DateOnly.FromDateTime(DateTime.Today));
    }
}

/// <summary>A tool result designed for the model to read: what it needs to answer, and nothing more.</summary>
public sealed record OrderLookupResult(string OrderNumber, bool Found, OrderDetails? Details)
{
    public static OrderLookupResult NotFound(string orderNumber) => new(orderNumber, false, null);

    public static OrderLookupResult FromOrder(Order order) => new(order.OrderId, true, OrderDetails.From(order));
}

public sealed record OrderDetails(
    string Status,
    DateOnly OrderDate,
    string ShippingMethod,
    string? TrackingNumber,
    DateOnly? EstimatedDelivery,
    DateOnly? DeliveredOn,
    IReadOnlyList<OrderItem> Items)
{
    public static OrderDetails From(Order order) => new(
        order.Status.ToString(), order.OrderDate, order.ShippingMethod, order.TrackingNumber,
        order.EstimatedDelivery, order.DeliveredOn,
        order.Lines.Select(l => new OrderItem(l.ProductId, l.ProductName, l.Quantity)).ToList());
}

public sealed record OrderItem(string ProductId, string Name, int Quantity);
