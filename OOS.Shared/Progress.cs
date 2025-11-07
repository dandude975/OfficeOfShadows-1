using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace OOS.Shared
{
    public class Progress
    {
        public string Checkpoint { get; set; } = "intro";   // "intro" before the video
        public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
        public Dictionary<string, bool> Flags { get; set; } = new();

        public static string FilePath => SharedPaths.ProgressFile;

        public static bool Exists() => File.Exists(FilePath);

        public static Progress Load()
        {
            if (!Exists()) return new Progress();
            try
            {
                var json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<Progress>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new Progress();
            }
            catch { return new Progress(); }
        }

        public void Save()
        {
            UpdatedUtc = DateTime.UtcNow;
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
        }

        public static void Reset()
        {
            try { if (Exists()) File.Delete(FilePath); } catch { /* ignore */ }
        }

        public bool IsAtOrBeyond(string checkpoint) => CheckpointOrder.IndexOf(Checkpoint) >= CheckpointOrder.IndexOf(checkpoint);

        public static readonly List<string> CheckpointOrder = new()
        {
            "intro",
            "video_played",
            "tools_opened",
            "first_email_read",
            "vpn_connected",
            "terminal_scan_done",
            "report_compiled",
            "game_complete"
        };
    }
}
