using System.ComponentModel;
using Northwind.Shared.Domain;
using Northwind.Shared.Tools;

namespace Ch13.ExcessiveAgency;

/// <summary>
/// An order tool bound to the signed-in customer, as Chapter 6.3 recommends. The model can ask
/// for any order number; it only ever sees orders that belong to the current customer.
/// </summary>
public sealed class CustomerOrderTools(NorthwindStore store, ICurrentCustomer currentCustomer)
{
    [Description("Gets the status and items of one of the signed-in customer's orders.")]
    public OrderLookupResult GetMyOrder(
        [Description("The order number, in the format NW-#####.")] string orderNumber)
    {
        Order? order = store.FindOrder(orderNumber.Trim().ToUpperInvariant());

        // Another customer's order is reported exactly like a missing one.
        return order is null || order.CustomerId != currentCustomer.CustomerId
            ? OrderLookupResult.NotFound(orderNumber)
            : OrderLookupResult.FromOrder(order);
    }
}
