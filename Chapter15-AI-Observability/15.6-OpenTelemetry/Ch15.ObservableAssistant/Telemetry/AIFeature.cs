namespace Ch15.ObservableAssistant.Telemetry;

/// <summary>
/// An ambient record of which feature is making model calls (Chapter 15.4). It flows through
/// asynchronous calls, so middleware deep in the chat client pipeline can tag metrics with it.
/// </summary>
public static class AIFeature
{
    private static readonly AsyncLocal<string?> CurrentFeature = new();

    public static string? Current => CurrentFeature.Value;

    /// <summary>Sets the current feature until the returned scope is disposed.</summary>
    public static IDisposable Begin(string feature)
    {
        string? previous = CurrentFeature.Value;
        CurrentFeature.Value = feature;
        return new Scope(previous);
    }

    private sealed class Scope(string? previous) : IDisposable
    {
        public void Dispose() => CurrentFeature.Value = previous;
    }
}
