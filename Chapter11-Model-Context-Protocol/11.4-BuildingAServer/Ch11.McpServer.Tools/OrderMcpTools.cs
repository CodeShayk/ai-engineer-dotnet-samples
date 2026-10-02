using System.ComponentModel;
using ModelContextProtocol.Server;
using Northwind.Shared.Domain;
using Northwind.Shared.Tools;

namespace Ch11.McpServer.Tools;

/// <summary>
/// The order tools exposed over MCP (Chapter 11.4). They wrap the same domain services as the
/// Northwind Assist tools from Chapter 6. Both are read-only and safe to repeat, which the
/// annotations tell hosts.
/// </summary>
[McpServerToolType]
public sealed class OrderMcpTools(NorthwindStore store, ReturnPolicyRules returnRules)
{
    [McpServerTool(Name = "get_order_status", ReadOnly = true, Idempotent = true)]
    [Description("Gets the status, items and delivery details of a Northwind Traders order.")]
    public OrderLookupResult GetOrderStatus(
        [Description("The order number, in the format NW-##### (for example NW-10249).")] string orderNumber)
    {
        Order? order = store.FindOrder(orderNumber.Trim().ToUpperInvariant());
        return order is null ? OrderLookupResult.NotFound(orderNumber) : OrderLookupResult.FromOrder(order);
    }

    [McpServerTool(Name = "check_return_eligibility", ReadOnly = true, Idempotent = true)]
    [Description("Checks whether an item from a Northwind order can be returned, and the last date to return it.")]
    public ReturnEligibility CheckReturnEligibility(
        [Description("The order number, in the format NW-#####.")] string orderNumber,
        [Description("The product ID of the item, for example P01. Get this from get_order_status.")] string productId,
        [Description("True if the customer has opened or used the item.")] bool opened)
    {
        Order? order = store.FindOrder(orderNumber.Trim().ToUpperInvariant());
        return order is null
            ? ReturnEligibility.NotEligible($"Order {orderNumber} was not found.")
            : returnRules.Evaluate(order, productId, opened, DateOnly.FromDateTime(DateTime.Today));
    }
}
