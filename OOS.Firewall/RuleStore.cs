using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace OOS.Firewall
{
    public enum RuleType
    {
        IpOrCidr,
        Port
    }

    public sealed class Rule
    {
        public bool Enabled { get; set; } = true;
        public RuleType Type { get; set; } = RuleType.IpOrCidr;

        /// <summary>
        /// For IpOrCidr: string like "203.0.113.5" or "203.0.113.0/24"
        /// For Port:     stringified port ("443")
        /// </summary>
        public string Value { get; set; } = "";

        /// <summary>Display only (TCP/UDP/etc). Not used by matcher yet.</summary>
        public string Protocol { get; set; } = "TCP";

        public string Notes { get; set; } = "";
        public DateTime AddedOn { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Persistent store for firewall rules.
    /// </summary>
    public sealed class RuleStore
    {
        public ObservableCollection<Rule> Rules { get; private set; } = new();

        private readonly string _path;

        public RuleStore(string path)
        {
            _path = path ?? throw new ArgumentNullException(nameof(path));
            Load();
        }

        public void AddIpRule(string ipOrCidr, string notes = "")
        {
            Rules.Add(new Rule
            {
                Enabled = true,
                Type = RuleType.IpOrCidr,
                Value = ipOrCidr.Trim(),
                Notes = notes?.Trim() ?? "",
                AddedOn = DateTime.Now
            });
        }

        public void AddPortRule(int port, string notes = "")
        {
            Rules.Add(new Rule
            {
                Enabled = true,
                Type = RuleType.Port,
                Value = port.ToString(),
                Notes = notes?.Trim() ?? "",
                AddedOn = DateTime.Now
            });
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                var json = JsonSerializer.Serialize(Rules, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_path, json);
            }
            catch
            {
                // non-fatal
            }
        }

        public void Load()
        {
            try
            {
                if (!File.Exists(_path)) return;
                var json = File.ReadAllText(_path);
                var loaded = JsonSerializer.Deserialize<Rule[]>(json);
                if (loaded != null)
                    Rules = new ObservableCollection<Rule>(loaded);
            }
            catch
            {
                // keep empty
                Rules = new ObservableCollection<Rule>();
            }
        }
    }
}
