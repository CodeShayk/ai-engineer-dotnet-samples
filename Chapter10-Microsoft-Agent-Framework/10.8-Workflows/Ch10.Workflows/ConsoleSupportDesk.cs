namespace Ch10.Workflows;

/// <summary>A supervisor deciding refund approvals at the console.</summary>
public sealed class ConsoleSupportDesk
{
    public Task<RefundDecision> DecideAsync(RefundApprovalRequest approval, CancellationToken cancellationToken)
    {
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine();
        Console.WriteLine("SUPERVISOR APPROVAL REQUIRED");
        Console.WriteLine($"  Request: {approval.RequestId}  Order: {approval.OrderNumber}  Item: {approval.ProductId}");
        Console.WriteLine($"  Amount:  {approval.Amount:0.00}  Reason: {approval.Reason}");
        Console.Write("  Approve? [y/N]: ");
        Console.ResetColor();

        bool approved = Console.ReadLine()?.Trim().StartsWith("y", StringComparison.OrdinalIgnoreCase) == true;
        Console.WriteLine();

        return Task.FromResult(new RefundDecision(approval.RequestId, approved, approved ? null : "Declined by the supervisor"));
    }
}
