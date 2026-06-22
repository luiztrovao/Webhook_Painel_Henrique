using System;
using System.IO;
using Serilog;
using ILogger = Serilog.ILogger;

namespace WebhookService.Services;

public static class FileLogger
{
    private static readonly ILogger _logger;

    static FileLogger()
    {
        var logDirectory = "logs";
        if (!Directory.Exists(logDirectory))
        {
            Directory.CreateDirectory(logDirectory);
        }

        _logger = new LoggerConfiguration()
            .WriteTo.File(
                Path.Combine(logDirectory, "webhook-log-.txt"),
                rollingInterval: RollingInterval.Day,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} UTC] [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }

    public static void LogInfo(string message)
    {
        _logger.Information(message);
    }

    public static void LogError(string message, Exception? ex = null)
    {
        if (ex != null)
        {
            _logger.Error(ex, message);
        }
        else
        {
            _logger.Error(message);
        }
    }
}
