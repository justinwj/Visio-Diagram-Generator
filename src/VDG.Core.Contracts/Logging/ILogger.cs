#if NETSTANDARD2_0
namespace VDG.Core.Contracts.Logging
{
    public interface ILogger
    {
        void Log(LogLevel level, string message, Exception? ex = null);

        // No default interface impls on netstandard2.0
        void Debug(string message);
        void Info(string message);
        void Warn(string message, Exception? ex = null);
        void Error(string message, Exception? ex = null);
        void Critical(string message, Exception? ex = null);
    }
}
#else
namespace VDG.Core.Contracts.Logging
{
    public interface ILogger
    {
        void Log(LogLevel level, string message, Exception? ex = null);

        // Convenience defaults (OK on net8.0+)
        void Debug(string message) => Log(LogLevel.Debug, message);
        void Info(string message)  => Log(LogLevel.Info, message);
        void Warn(string message, Exception? ex = null) => Log(LogLevel.Warn, message, ex);
        void Error(string message, Exception? ex = null) => Log(LogLevel.Error, message, ex);
        void Critical(string message, Exception? ex = null) => Log(LogLevel.Critical, message, ex);
    }
}
#endif
