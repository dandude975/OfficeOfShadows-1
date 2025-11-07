using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace OOS.Shared
{
    public class TimelineItem
    {
        public string Id { get; set; } = "";
        public string? OnType { get; set; }          // trigger message type or "start"
        public string? Matches { get; set; }         // optional text/regex
        public List<TimelineAction> Do { get; set; } = new();
    }

    public class TimelineAction
    {
        public string Act { get; set; } = "";        // show_toast, drop_file, spawn_window, play_audio, etc.
        public Dictionary<string, string>? Args { get; set; }
    }

    public static class Timeline
    {
        public static List<TimelineItem> LoadOrEmpty()
        {
            var path = SharedPaths.TimelineFile;
            if (!File.Exists(path)) return new();
            try
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<List<TimelineItem>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
            }
            catch { return new(); }
        }
    }
}
