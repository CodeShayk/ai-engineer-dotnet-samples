using System.ComponentModel;
using Northwind.Shared.Domain;
using Northwind.Shared.Tools;

namespace Ch06.ServiceTools;

/// <summary>
/// Tools registered as a scoped service and created per request (Chapter 6.3). Note what the
/// methods do not take as a parameter: the customer ID. It comes from the signed-in user.
/// </summary>
public sealed class CustomerSupportTools(
    IOrderService orders,
    ICurrentCustomer currentCustomer)
{
    [Description("Lists the signed-in customer's five most recent orders with their status.")]
    public async Task<IReadOnlyList<OrderSummary>> GetMyRecentOrdersAsync(CancellationToken cancellationToken) =>
        await orders.GetRecentOrdersAsync(currentCustomer.CustomerId, take: 5, cancellationToken);

    [Description("Gets details of one of the signed-in customer's orders.")]
    public async Task<OrderLookupResult> GetMyOrderAsync(
        [Description("The order number, in the format NW-#####.")] string orderNumber,
        CancellationToken cancellationToken) =>
        await orders.GetOrderForCustomerAsync(currentCustomer.CustomerId, orderNumber, cancellationToken);
}
