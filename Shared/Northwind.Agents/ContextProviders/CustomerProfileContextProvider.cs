using Microsoft.Agents.AI;
using Northwind.Shared.Domain;

namespace Northwind.Agents.ContextProviders;

/// <summary>
/// Tells the agent who it is talking to on every run (Chapter 10.6). The instructions are
/// transient: they apply to the current model call and are never stored in the history,
/// so dates and order statuses are always current.
/// </summary>
public sealed class CustomerProfileContextProvider(NorthwindStore store, ICurrentCustomer currentCustomer)
    : AIContextProvider
{
    protected override ValueTask<AIContext> ProvideAIContextAsync(
        InvokingContext context, CancellationToken cancellationToken = default)
    {
        Customer? customer = store.FindCustomer(currentCustomer.CustomerId);
        if (customer is null)
        {
            return ValueTask.FromResult(new AIContext());
        }

        string recentOrders = string.Join("; ", store.OrdersForCustomer(customer.CustomerId)
            .OrderByDescending(o => o.OrderDate)
            .Take(3)
            .Select(o => $"{o.OrderId} ({o.Status}, ordered {o.OrderDate:yyyy-MM-dd})"));

        return ValueTask.FromResult(new AIContext
        {
            Instructions = $"""
                <customer_profile>
                name: {customer.Name}
                country: {customer.Country}
                loyalty_tier: {customer.LoyaltyTier}
                recent_orders: {recentOrders}
                today: {DateTime.UtcNow:yyyy-MM-dd}
                </customer_profile>
                """
        });
    }
}
