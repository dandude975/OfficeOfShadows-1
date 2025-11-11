using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;

namespace OOS.Firewall
{
    /// <summary>
    /// Persistent log store for the Firewall app (unique type names to avoid collisions).
    /// </summary>
    public sealed class FirewallActivityStore
    {
        public ObservableCollection<FirewallActivityEntry> Items { get; private set; } = new();
        public bool Dirty { get; set; } = false;

        private readonly string _path;

        public FirewallActivityStore(string path)
        {
            _path = path ?? throw new ArgumentNullException(nameof(path));
            Load();
        }

        public void Add(FirewallActivityEntry entry)
        {
            Items.Add(entry);
            Dirty = true;
        }

        public void Load()
        {
            try
            {
                if (!File.Exists(_path)) return;

                string json = File.ReadAllText(_path);
                var loaded = JsonSerializer.Deserialize<FirewallActivityEntry[]>(json);
                if (loaded != null)
                    Items = new ObservableCollection<FirewallActivityEntry>(loaded);
            }
            catch
            {
                // Non-fatal; keep empty list on any parse error.
                Items = new ObservableCollection<FirewallActivityEntry>();
            }
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                string json = JsonSerializer.Serialize(Items, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_path, json);
                Dirty = false;
            }
            catch
            {
                // Non-fatal; ignore IO errors for gameplay.
            }
        }
    }

    /// <summary>
    /// One firewall activity record (unique type name to avoid collisions).
    /// </summary>
    public sealed class FirewallActivityEntry
    {
        public DateTime Timestamp { get; set; }
        public string LocalIP { get; set; } = "";
        public string LocalPort { get; set; } = "";
        public string RemoteIP { get; set; } = "";
        public string RemotePort { get; set; } = "";
        public string Matched { get; set; } = "";
    }
}
