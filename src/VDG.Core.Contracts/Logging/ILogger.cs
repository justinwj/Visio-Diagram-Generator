namespace VDG.Core.Logging;

/// <summary>Minimal logger abstraction to avoid external dependencies.</summary>
public interface ILogger
{
    void Log(LogLevel level, string message, Exception? exception = null);

    // Convenience helpers with default interface implementations.
    void Trace(string message) => Log(LogLevel.Trace, message);
    void Debug(string message) => Log(LogLevel.Debug, message);
    void Info(string message)  => Log(LogLevel.Information, message);
    void Warn(string message)  => Log(LogLevel.Warning, message);
    void Error(string message, Exception? ex = null) => Log(LogLevel.Error, message, ex);
    void Critical(string message, Exception? ex = null) => Log(LogLevel.Critical, message, ex);
}
