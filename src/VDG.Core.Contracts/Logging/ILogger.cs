namespace VDG.Core.Logging;

public interface ILogger
{
    bool IsEnabled(LogLevel level);
    void Log(LogLevel level, string message, Exception? ex = null);
}
