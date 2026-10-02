namespace Ch05.TypedResponses;

/// <summary>
/// Stands in for the routing system that would consume triage results. It prints the
/// decision instead of creating a ticket in a real queue.
/// </summary>
public sealed class SupportRouter
{
    public Task RouteAsync(TicketTriage triage, CancellationToken cancellationToken = default)
    {
        string queue = triage.Category switch
        {
            TicketCategory.OrderStatus => "Order status",
            TicketCategory.ReturnsAndRefunds => "Returns and refunds",
            TicketCategory.Billing => "Billing",
            TicketCategory.ProductQuestion => "Product questions",
            TicketCategory.AccountAndPrivacy => "Account and privacy",
            _ => "General"
        };

        Console.WriteLine($"Routed to the '{queue}' queue with {triage.Urgency} priority.");
        return Task.CompletedTask;
    }

    public Task RouteToHumanAsync(string message, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Routed to a human for manual triage ({message.Length} characters).");
        return Task.CompletedTask;
    }
}
