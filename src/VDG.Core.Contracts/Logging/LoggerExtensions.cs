namespace VDG.Core.Logging;

public static class LoggerExtensions
{
    public static void Trace(this ILogger logger, string message)
        => logger.Log(LogLevel.Trace, message);

    public static void Debug(this ILogger logger, string message)
        => logger.Log(LogLevel.Debug, message);

    public static void Info(this ILogger logger, string message)
        => logger.Log(LogLevel.Information, message);

    public static void Warn(this ILogger logger, string message, Exception? ex = null)
        => logger.Log(LogLevel.Warning, message, ex);

    public static void Error(this ILogger logger, string message, Exception? ex = null)
        => logger.Log(LogLevel.Error, message, ex);

    public static void Critical(this ILogger logger, string message, Exception? ex = null)
        => logger.Log(LogLevel.Critical, message, ex);
}