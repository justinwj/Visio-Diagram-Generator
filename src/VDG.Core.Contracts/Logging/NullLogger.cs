namespace VDG.Core.Logging;

/// <summary>No-op logger useful for tests and default wiring.</summary>
public sealed class NullLogger : ILogger
{
    public static readonly NullLogger Instance = new();
    private NullLogger() { }
    public void Log(LogLevel level, string message, Exception? exception = null) { /* intentionally no-op */ }
}
