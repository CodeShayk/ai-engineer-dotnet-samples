using Microsoft.Extensions.Logging;

namespace Northwind.Shared.AI;

/// <summary>Small helpers that keep the console samples' output consistent and readable.</summary>
public static class SampleConsole
{
    public static void Header(string title, AIProviderOptions? options = null)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(title);
        Console.WriteLine(new string('=', title.Length));
        Console.ResetColor();
        if (options is not null)
        {
            Console.WriteLine($"Provider: {options}");
        }

        Console.WriteLine();
    }

    public static void Section(string title)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"--- {title} ---");
        Console.ResetColor();
    }

    public static void Note(string text)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine(text);
        Console.ResetColor();
    }

    /// <summary>
    /// Creates a logger factory that writes to the console synchronously, so log entries appear
    /// in order with the sample's own output. (The standard console logger writes on a
    /// background thread, which is right for servers but confusing in a step-by-step demo.)
    /// </summary>
    public static ILoggerFactory CreateLoggerFactory(LogLevel minimumLevel = LogLevel.Information) =>
        LoggerFactory.Create(builder => builder
            .SetMinimumLevel(minimumLevel)
            .AddProvider(new SampleConsoleLoggerProvider()));

    /// <summary>Reads a line, returning null when the user enters nothing or input is redirected and exhausted.</summary>
    public static string? Prompt(string label)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write($"{label}: ");
        Console.ResetColor();
        string? line = Console.ReadLine();
        return string.IsNullOrWhiteSpace(line) ? null : line.Trim();
    }
}
