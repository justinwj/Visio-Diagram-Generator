namespace VDG.Core.Logging;

/// <summary>Very small console logger for debugging during development.</summary>
public sealed class ConsoleLogger : ILogger
{
    public void Log(LogLevel level, string message, Exception? exception = null)
    {
        var prefix = $"[{DateTime.Now:HH:mm:ss}] {level,11}: ";
        Console.WriteLine(prefix + message);
        if (exception is not null)
        {
            Console.WriteLine(exception);
        }
    }
}
