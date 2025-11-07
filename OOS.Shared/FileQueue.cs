using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace OOS.Shared
{
    public static class FileQueue
    {
        private static readonly object _lock = new();

        public static void Enqueue(GameMessage msg)
        {
            var line = JsonSerializer.Serialize(msg) + Environment.NewLine;
            var path = Path.Combine(SharedPaths.Queue, $"inbox_{DateTime.UtcNow:yyyyMMdd}.jsonl");
            lock (_lock) File.AppendAllText(path, line, Encoding.UTF8);
        }

        // Simple follower that tails today’s file; apps can watch folder with FileSystemWatcher
        public static FileSystemWatcher CreateWatcher(Action<GameMessage> onMessage, string? pattern = null)
        {
            var watcher = new FileSystemWatcher(SharedPaths.Queue, pattern ?? "inbox_*.jsonl")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime,
                IncludeSubdirectories = false,
                EnableRaisingEvents = true
            };

            watcher.Changed += (_, __) => TryReadTail(onMessage);
            watcher.Created += (_, __) => TryReadTail(onMessage);

            // initial sweep (optional)
            TryReadTail(onMessage);
            return watcher;
        }

        private static long _lastLen = 0;
        private static void TryReadTail(Action<GameMessage> onMessage)
        {
            try
            {
                var file = LatestInboxFile();
                if (file == null) return;
                using var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                if (fs.Length < _lastLen) _lastLen = 0; // rotated
                fs.Seek(_lastLen, SeekOrigin.Begin);
                using var sr = new StreamReader(fs, Encoding.UTF8);
                string? line;
                while ((line = sr.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    try { onMessage(GameMessage.FromJson(line)); } catch { /* ignore bad lines */ }
                }
                _lastLen = fs.Length;
            }
            catch { /* ignore transient IO */ }
        }

        private static string? LatestInboxFile()
        {
            var files = Directory.GetFiles(SharedPaths.Queue, "inbox_*.jsonl");
            if (files.Length == 0) return null;
            Array.Sort(files, StringComparer.Ordinal);
            return files[^1];
        }
    }
}
