using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Ch12.SupportHandoff;

/// <summary>A supervisor reviewing the refund agent's requests at the console.</summary>
public sealed class ConsoleSupervisor
{
    public Task<bool> ReviewAsync(FunctionCallContent call)
    {
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine();
        Console.WriteLine($"[Supervisor approval requested: {call.Name}({JsonSerializer.Serialize(call.Arguments)})]");
        Console.Write("Approve? [y/N]: ");
        Console.ResetColor();

        bool approved = Console.ReadLine()?.Trim().StartsWith("y", StringComparison.OrdinalIgnoreCase) == true;
        Console.WriteLine(approved ? "[Approved]" : "[Declined]");
        return Task.FromResult(approved);
    }
}
