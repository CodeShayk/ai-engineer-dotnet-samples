using System.Text.Json;

namespace Ch10.ToolsAndMiddleware;

/// <summary>
/// Stands in for the support desk's approval queue. A real application would persist the
/// session, put the request in a work queue and resume the run when a person decides,
/// possibly hours later. Here, a person at the console decides immediately.
/// </summary>
public sealed class ConsoleSupportDesk
{
    public Task<bool> RequestApprovalAsync(string toolName, IDictionary<string, object?>? arguments, CancellationToken cancellationToken)
    {
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine();
        Console.WriteLine("SUPPORT DESK: approval required");
        Console.WriteLine($"  Tool:      {toolName}");
        Console.WriteLine($"  Arguments: {JsonSerializer.Serialize(arguments ?? new Dictionary<string, object?>())}");
        Console.Write("  Approve? [y/N]: ");
        Console.ResetColor();

        bool approved = Console.ReadLine()?.Trim().StartsWith("y", StringComparison.OrdinalIgnoreCase) == true;
        Console.WriteLine(approved ? "  Approved." : "  Declined.");
        Console.WriteLine();

        return Task.FromResult(approved);
    }
}
