using Northwind.Shared.Domain;
using Northwind.Shared.Tools;

namespace Ch06.ServiceTools;

/// <summary>A summary row: the fields that help answer questions about recent orders, and no more.</summary>
public sealed record OrderSummary(string OrderNumber, DateOnly OrderDate, string Status, int ItemCount, decimal Total);

/// <summary>
/// The order service the tools depend on. In a real application this would wrap a
/// DbContext or an internal API; every method takes the customer explicitly.
/// </summary>
public interface IOrderService
{
    Task<IReadOnlyList<OrderSummary>> GetRecentOrdersAsync(string customerId, int take, CancellationToken cancellationToken);

    Task<OrderLookupResult> GetOrderForCustomerAsync(string customerId, string orderNumber, CancellationToken cancellationToken);
}

/// <summary>An in-memory implementation with a small delay that stands in for a database round trip.</summary>
public sealed class InMemoryOrderService(NorthwindStore store) : IOrderService
{
    private static readonly TimeSpan SimulatedLatency = TimeSpan.FromMilliseconds(50);

    public async Task<IReadOnlyList<OrderSummary>> GetRecentOrdersAsync(
        string customerId, int take, CancellationToken cancellationToken)
    {
        await Task.Delay(SimulatedLatency, cancellationToken);

        return store.OrdersForCustomer(customerId)
            .OrderByDescending(o => o.OrderDate)
            .Take(take)
            .Select(o => new OrderSummary(o.OrderId, o.OrderDate, o.Status.ToString(), o.Lines.Sum(l => l.Quantity), o.Total))
            .ToList();
    }

    public async Task<OrderLookupResult> GetOrderForCustomerAsync(
        string customerId, string orderNumber, CancellationToken cancellationToken)
    {
        await Task.Delay(SimulatedLatency, cancellationToken);

        Order? order = store.FindOrder(orderNumber.Trim().ToUpperInvariant());

        // Another customer's order is reported exactly like a missing one, so the
        // response reveals nothing about orders on other accounts.
        return order is null || order.CustomerId != customerId
            ? OrderLookupResult.NotFound(orderNumber)
            : OrderLookupResult.FromOrder(order);
    }
}
