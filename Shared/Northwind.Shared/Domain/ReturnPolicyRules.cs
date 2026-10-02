namespace Northwind.Shared.Domain;

/// <summary>The result of a return eligibility check, designed to be read by a model as a tool result.</summary>
public sealed record ReturnEligibility(bool Eligible, string Reason, DateOnly? Deadline)
{
    public static ReturnEligibility Allowed(string reason, DateOnly deadline) => new(true, reason, deadline);

    public static ReturnEligibility NotEligible(string reason) => new(false, reason, null);
}

/// <summary>
/// Northwind's return rules, implemented in code rather than left to the model (Chapter 6).
/// These mirror the Returns Policy and Electronics Returns documents in the policy library.
/// </summary>
public sealed class ReturnPolicyRules(NorthwindStore store)
{
    public const int StandardWindowDays = 30;
    public const int OpenedElectronicsWindowDays = 14;

    public ReturnEligibility Evaluate(Order order, string productId, bool opened, DateOnly today)
    {
        OrderLine? line = order.Lines.FirstOrDefault(l => l.ProductId.Equals(productId.Trim(), StringComparison.OrdinalIgnoreCase));
        if (line is null)
        {
            return ReturnEligibility.NotEligible($"Order {order.OrderId} does not contain product {productId}.");
        }

        if (order.Status == OrderStatus.Refunded)
        {
            return ReturnEligibility.NotEligible($"The items in order {order.OrderId} have already been refunded.");
        }

        if (order.DeliveredOn is not DateOnly deliveredOn)
        {
            return ReturnEligibility.NotEligible(
                $"Items can only be returned after delivery. Order {order.OrderId} is currently {order.Status}.");
        }

        Product? product = store.FindProduct(line.ProductId);

        if (product?.Category == ProductCategory.Grocery)
        {
            return ReturnEligibility.NotEligible("Groceries cannot be returned unless they arrive damaged or faulty.");
        }

        if (product?.FinalSale == true)
        {
            return ReturnEligibility.NotEligible("Final sale items cannot be returned unless they are damaged or faulty.");
        }

        bool openedElectronics = opened && product?.Category == ProductCategory.Electronics;
        int windowDays = openedElectronics ? OpenedElectronicsWindowDays : StandardWindowDays;
        DateOnly deadline = deliveredOn.AddDays(windowDays);

        if (today > deadline)
        {
            return ReturnEligibility.NotEligible(
                $"The {windowDays}-day return window for this item ended on {deadline:d MMMM yyyy}.");
        }

        return ReturnEligibility.Allowed(
            openedElectronics
                ? $"Opened electronics can be returned within {OpenedElectronicsWindowDays} days of delivery."
                : $"Items can be returned within {StandardWindowDays} days of delivery.",
            deadline);
    }
}
