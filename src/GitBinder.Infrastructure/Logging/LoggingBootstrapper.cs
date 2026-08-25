using GitBinder.Application.Common;
using Serilog;

namespace GitBinder.Infrastructure.Logging;

/// <summary>
/// 日志初始化器，负责创建 Serilog logger 并脱敏。
/// </summary>
public static class LoggingBootstrapper
{
    public static ILogger CreateLogger(string logDirectory)
    {
        Directory.CreateDirectory(logDirectory);
        var logPath = Path.Combine(logDirectory, "gitbinder-.log");

        return new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                logPath,
                rollingInterval: RollingInterval.Day,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }
}