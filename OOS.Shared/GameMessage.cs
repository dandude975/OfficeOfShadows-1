using System;
using System.Text.Json;

namespace OOS.Shared
{
    public class GameMessage
    {
        public string Type { get; set; } = "";   // e.g. "terminal.command", "email.read"
        public string From { get; set; } = "";   // "Terminal", "Email", "Game"
        public object? Data { get; set; }        // any payload
        public DateTime Ts { get; set; } = DateTime.UtcNow;

        public override string ToString() => JsonSerializer.Serialize(this);
        public static GameMessage FromJson(string json) =>
            JsonSerializer.Deserialize<GameMessage>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }
}
