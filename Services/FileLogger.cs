using System;
using System.IO;
using System.Threading.Tasks;

namespace WebhookService.Services;

public static class FileLogger
{
    private static readonly string LogDirectory = "logs";
    private static readonly string LogFilePath = Path.Combine(LogDirectory, "webhook-log.txt");
    private static readonly object _lock = new object();

    static FileLogger()
    {
        if (!Directory.Exists(LogDirectory))
        {
            Directory.CreateDirectory(LogDirectory);
        }
    }

    public static void LogInfo(string message)
    {
        Log("INFO", message);
    }

    public static void LogError(string message, Exception? ex = null)
    {
        var errorMessage = ex == null ? message : $"{message}\nException: {ex.Message}\nStackTrace: {ex.StackTrace}";
        Log("ERROR", errorMessage);
    }

    private static void Log(string level, string message)
    {
        try
        {
            var logEntry = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC] [{level}] {message}{Environment.NewLine}";
            
            lock (_lock)
            {
                File.AppendAllText(LogFilePath, logEntry);
            }
        }
        catch
        {
            // Fallback: If writing to the file fails, we have nowhere else to log without throwing and crashing the app.
        }
    }
}
