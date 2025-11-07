// OOS.Shared/SharedLogger.cs
using System;
using System.IO;

namespace OOS.Shared
{
    public static class SharedLogger
    {
        private static readonly object _lock = new();
        public static void Info(string msg) => Write("INFO", msg);
        public static void Warn(string msg) => Write("WARN", msg);
        public static void Error(string msg) => Write("ERR ", msg);

        private static void Write(string level, string msg)
        {
            var line = $"{DateTime.UtcNow:O} [{level}] {msg}{Environment.NewLine}";
            lock (_lock)
            {
                File.AppendAllText(Path.Combine(SharedPaths.Logs, $"game_{DateTime.UtcNow:yyyyMMdd}.log"), line);
            }
        }
    }
}
