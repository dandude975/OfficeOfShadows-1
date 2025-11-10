using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OOS.Shared
{
    /// <summary>
    /// Lightweight app settings persisted under %LocalAppData%\OfficeOfShadows\settings.json
    /// </summary>
    public sealed class Settings
    {
        /// <summary>
        /// When true, the Desktop sandbox folder opens in Explorer at app start.
        /// </summary>
        public bool OpenSandboxOnStart { get; set; } = true;

        /// <summary>
        /// Internal: tracks whether a first-run toast or similar was shown.
        /// </summary>
        public bool FirstRunToastShown { get; set; } = false;
    }

    public static class SettingsStore
    {
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private static string GetAppDataRoot()
        {
            var root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "OfficeOfShadows");
            Directory.CreateDirectory(root);
            return root;
        }

        private static string GetSettingsPath() => Path.Combine(GetAppDataRoot(), "settings.json");

        public static Settings Load()
        {
            try
            {
                var path = GetSettingsPath();
                if (!File.Exists(path))
                    return new Settings();

                var json = File.ReadAllText(path);
                var loaded = JsonSerializer.Deserialize<Settings>(json, _jsonOptions);
                return loaded ?? new Settings();
            }
            catch
            {
                // Corrupt or unreadable settings — fall back to defaults.
                return new Settings();
            }
        }

        public static void Save(Settings settings)
        {
            var path = GetSettingsPath();
            var tmp = path + ".tmp";

            var json = JsonSerializer.Serialize(settings, _jsonOptions);
            File.WriteAllText(tmp, json);

            // Atomic replace to avoid corruption on crash/power loss.
            if (File.Exists(path))
            {
                File.Replace(tmp, path, path + ".bak", ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(tmp, path);
            }
        }
    }
}
