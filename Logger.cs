using System;
using System.IO;

public static class Logger
{
    private static readonly object _lock = new();
    private static readonly string LogFilePath = "skin_validator.log";

    public static void Info(string message)
        => Write("INFO", message);

    public static void Warn(string message)
        => Write("WARN", message);

    public static void Error(string message)
        => Write("ERROR", message);

    private static void Write(string level, string message)
    {
        string log = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";

        lock (_lock)
        {
            // コンソール
            Console.WriteLine(log);

            // ファイル
            File.AppendAllText(LogFilePath, log + Environment.NewLine);
        }
    }
}