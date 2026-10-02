using System.Text.Json;

namespace Ch06.ManualInvocation;

/// <summary>Records every tool call before it runs. A real audit log would write to durable storage.</summary>
public sealed class AuditLog
{
    private readonly List<string> _entries = [];

    public IReadOnlyList<string> Entries => _entries;

    public void Record(string toolName, IDictionary<string, object?>? arguments)
    {
        string entry = $"{DateTimeOffset.Now:HH:mm:ss} {toolName} {JsonSerializer.Serialize(arguments ?? new Dictionary<string, object?>())}";
        _entries.Add(entry);

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"[audit] {entry}");
        Console.ResetColor();
    }
}
