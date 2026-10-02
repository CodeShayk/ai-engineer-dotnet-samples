using Microsoft.Extensions.Logging;

namespace Northwind.Shared.AI;

/// <summary>
/// A minimal logger provider for the console samples. It writes each entry immediately, on the
/// calling thread, as a single line such as <c>[info] RefundTools: Refund request RF-1001 ...</c>.
/// Filtering is left to the logging infrastructure (SetMinimumLevel and AddFilter).
/// </summary>
public sealed class SampleConsoleLoggerProvider : ILoggerProvider
{
    private static readonly Lock Gate = new();

    public ILogger CreateLogger(string categoryName) => new SampleConsoleLogger(categoryName);

    public void Dispose() { }

    private sealed class SampleConsoleLogger(string categoryName) : ILogger
    {
        // "Northwind.Shared.Tools.RefundTools" is shown as "RefundTools".
        private readonly string _shortName = categoryName[(categoryName.LastIndexOf('.') + 1)..];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            string level = logLevel switch
            {
                LogLevel.Trace => "trce",
                LogLevel.Debug => "dbug",
                LogLevel.Information => "info",
                LogLevel.Warning => "warn",
                LogLevel.Error => "fail",
                _ => "crit"
            };

            lock (Gate)
            {
                Console.ForegroundColor = logLevel >= LogLevel.Warning ? ConsoleColor.Yellow : ConsoleColor.DarkCyan;
                Console.WriteLine($"[{level}] {_shortName}: {formatter(state, exception)}");
                if (exception is not null)
                {
                    Console.WriteLine($"       {exception.GetType().Name}: {exception.Message}");
                }

                Console.ResetColor();
            }
        }
    }
}
