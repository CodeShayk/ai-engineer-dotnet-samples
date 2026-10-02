using System.Text.Json;

namespace Ch06.ManualInvocation;

/// <summary>
/// Asks a support agent at the console to approve a tool call. A real application would not
/// block waiting for a person: it would store the conversation, put the request in a work
/// queue and resume when the agent responds (see Chapters 10 and 12).
/// </summary>
public sealed class ConsoleApprovals
{
    public Task<bool> AskAgentAsync(string toolName, IDictionary<string, object?>? arguments, CancellationToken cancellationToken)
    {
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine();
        Console.WriteLine("APPROVAL REQUIRED");
        Console.WriteLine($"  Tool:      {toolName}");
        Console.WriteLine($"  Arguments: {JsonSerializer.Serialize(arguments ?? new Dictionary<string, object?>())}");
        Console.Write("  Approve? [y/N]: ");
        Console.ResetColor();

        string? answer = Console.ReadLine();
        bool approved = answer?.Trim().StartsWith("y", StringComparison.OrdinalIgnoreCase) == true;

        Console.WriteLine(approved ? "  Approved." : "  Declined.");
        Console.WriteLine();
        return Task.FromResult(approved);
    }
}
