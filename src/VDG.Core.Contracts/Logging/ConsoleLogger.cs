namespace VDG.Core.Logging;

public sealed class ConsoleLogger : ILogger
{
    private readonly LogLevel _min;
    public ConsoleLogger(LogLevel min = LogLevel.Information) => _min = min;

    public bool IsEnabled(LogLevel level) => level >= _min;

    public void Log(LogLevel level, string message, Exception? ex = null)
    {
        if (!IsEnabled(level)) return;
        var ts = DateTime.Now.ToString("HH:mm:ss");
        var line = $"[{ts}] {level,11}: {message}";
        Console.WriteLine(line);
        if (ex is not null)
        {
            Console.WriteLine(ex.ToString());
        }
    }
}