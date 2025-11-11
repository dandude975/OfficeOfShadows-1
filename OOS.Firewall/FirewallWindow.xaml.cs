using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace OOS.Firewall
{
    public partial class FirewallWindow : Window
    {
        // Timers
        private readonly DispatcherTimer _pollTimer;
        private readonly DispatcherTimer _simTimer;

        // Data stores
        private readonly RuleStore _store;                        // your existing rules store
        private readonly FirewallActivityStore _activity;         // NEW unique type

        // Random + simple sim state
        private readonly Random _rng = new();
        private bool _simulateRealTraffic = true;

        // Optional callbacks (wired from OOS.Game)
        public Action<string, string>? AppBlockedCallback;        // (appName, reason)
        public Action<string>? AttackStartedCallback;             // "DoSStarted"/"DoSStopped"/"FileDelivered:<ip>"

        public FirewallWindow()
        {
            InitializeComponent();

            // Path roots
            string baseDir = ResolveDataRoot();
            _store = new RuleStore(System.IO.Path.Combine(baseDir, "FirewallRules.json"));
            _activity = new FirewallActivityStore(System.IO.Path.Combine(baseDir, "FirewallActivity.json"));

            // Bind UI
            RulesGrid.ItemsSource = _store.Rules;
            ActivityGrid.ItemsSource = _activity.Items;

            // Timers
            _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _pollTimer.Tick += (s, e) =>
            {
                if (AutoRefreshChk.IsChecked == true)
                    ScanActiveConnections();
            };
            _pollTimer.Start();

            _simTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _simTimer.Tick += (s, e) =>
            {
                if (_simulateRealTraffic)
                    GenerateSimulatedTraffic();
            };
            _simTimer.Start();
        }

        // ------------------------------------------------------------
        // Window chrome (drag & close)
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }
        private void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();

        // ------------------------------------------------------------
        // Rule actions
        private void AddRuleBtn_Click(object sender, RoutedEventArgs e)
        {
            var typeItem = RuleTypeCombo.SelectedItem as System.Windows.Controls.ComboBoxItem;
            var typeText = typeItem?.Content?.ToString() ?? "IP / CIDR";

            var value = RuleValueText.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(value))
            {
                MessageBox.Show("Enter a value (IP/CIDR or port).");
                return;
            }

            var note = RuleNoteText.Text?.Trim() ?? "";

            if (typeText.StartsWith("Port", StringComparison.OrdinalIgnoreCase))
            {
                if (!int.TryParse(value, out var port) || port <= 0 || port > 65535)
                {
                    MessageBox.Show("Invalid port.");
                    return;
                }

                _store.AddPortRule(port, note);
            }
            else
            {
                if (!TryParseCidrOrIp(value, out _, out _))
                {
                    MessageBox.Show("Invalid IP or CIDR.");
                    return;
                }
                _store.AddIpRule(value, note);
            }

            _store.Save();
            RulesGrid.Items.Refresh();
        }

        private void EnableRuleBtn_Click(object sender, RoutedEventArgs e)
        {
            if (RulesGrid.SelectedItem is Rule r) { r.Enabled = true; _store.Save(); RulesGrid.Items.Refresh(); }
        }
        private void DisableRuleBtn_Click(object sender, RoutedEventArgs e)
        {
            if (RulesGrid.SelectedItem is Rule r) { r.Enabled = false; _store.Save(); RulesGrid.Items.Refresh(); }
        }
        private void RemoveRuleBtn_Click(object sender, RoutedEventArgs e)
        {
            if (RulesGrid.SelectedItem is Rule r) { _store.Rules.Remove(r); _store.Save(); RulesGrid.Items.Refresh(); }
        }
        private void SaveRulesBtn_Click(object sender, RoutedEventArgs e) => _store.Save();

        // ------------------------------------------------------------
        // Activity actions
        private void ClearLogBtn_Click(object sender, RoutedEventArgs e)
        {
            _activity.Items.Clear();
            _activity.Dirty = true;
            _activity.Save();
            ActivityGrid.Items.Refresh();
        }
        private void ExportLogBtn_Click(object sender, RoutedEventArgs e)
        {
            _activity.Save();
            MessageBox.Show("Log exported (JSON) next to FirewallActivity.json.");
        }

        // ------------------------------------------------------------
        // Connection scanning (you may already have a more complete version)
        private void ScanActiveConnections()
        {
            // Keep it simple; this demo focuses on simulated traffic.
            // If you use real OS queries, append here and call AddActivity(...)
        }

        // ------------------------------------------------------------
        // Simulation
        private void GenerateSimulatedTraffic()
        {
            // Most entries: harmless traffic
            bool attacker = (_rng.NextDouble() < 0.15);
            string remoteIp = attacker ? GeneratePublicIp() : GeneratePrivateIp();
            int remotePort = attacker ? 445 : (_rng.Next(2) == 0 ? 80 : 443);

            // Check rules
            if (IsBlocked(remoteIp, remotePort, out string matched))
            {
                AddActivity("127.0.0.1", RandPort(), remoteIp, remotePort, $"Blocked by rule ({matched})");
                return;
            }

            // Log either benign or suspicious
            if (attacker)
            {
                // 50% probe, 50% file attempt
                if (_rng.NextDouble() < 0.5)
                {
                    AddActivity("127.0.0.1", RandPort(), remoteIp, remotePort, "Probe (ping)");
                }
                else
                {
                    AddActivity("127.0.0.1", RandPort(), remoteIp, remotePort, "Suspicious file transfer attempt (pending)");

                    // Escalate in a few seconds if still not blocked
                    var ip = remoteIp; var port = remotePort;
                    var when = DateTime.Now.AddSeconds(5);
                    Dispatcher.InvokeAsync(async () =>
                    {
                        while (DateTime.Now < when) await System.Threading.Tasks.Task.Delay(250);
                        if (!IsBlocked(ip, port, out _))
                        {
                            AddActivity("127.0.0.1", RandPort(), ip, port, "File delivered - requires Antivirus");
                            AttackStartedCallback?.Invoke("FileDelivered:" + ip);
                        }
                    });
                }
            }
            else
            {
                AddActivity("127.0.0.1", RandPort(), remoteIp, remotePort, "Normal traffic");
            }
        }

        // ------------------------------------------------------------
        // Helpers
        private void AddActivity(string localIp, int localPort, string remoteIp, int remotePort, string matched)
        {
            _activity.Add(new FirewallActivityEntry
            {
                Timestamp = DateTime.Now,
                LocalIP = localIp,
                LocalPort = localPort.ToString(),
                RemoteIP = remoteIp,
                RemotePort = remotePort.ToString(),
                Matched = matched
            });
            ActivityGrid.Items.Refresh();
            _activity.Save();
        }

        private int RandPort() => _rng.Next(1025, 65000);

        private static string GeneratePrivateIp()
        {
            var r = new Random();
            return (r.NextDouble() < 0.6)
                ? $"192.168.{r.Next(0, 5)}.{r.Next(2, 254)}"
                : $"10.{r.Next(0, 255)}.{r.Next(0, 255)}.{r.Next(1, 254)}";
        }

        private static string GeneratePublicIp()
        {
            var r = new Random();
            int a;
            do { a = r.Next(1, 255); } while (a is 10 or 127 or 172 or 192); // skip private/reserved blocks
            return $"{a}.{r.Next(1, 255)}.{r.Next(1, 255)}.{r.Next(1, 255)}";
        }

        private bool IsBlocked(string remoteIp, int remotePort, out string matchedRuleSummary)
        {
            matchedRuleSummary = "none";
            foreach (var r in _store.Rules)
            {
                if (!r.Enabled) continue;

                if (r.Type == RuleType.Port)
                {
                    if (int.TryParse(r.Value, out var p) && p == remotePort)
                    {
                        matchedRuleSummary = $"Port {p}";
                        return true;
                    }
                }
                else
                {
                    if (TryParseCidrOrIp(r.Value, out var network, out var mask)
                        && IsInSubnet(IPAddress.Parse(remoteIp), network, mask))
                    {
                        matchedRuleSummary = r.Value;
                        return true;
                    }
                }
            }
            return false;
        }

        // CIDR/IP parsing helpers (keep simple and local)
        private static bool TryParseCidrOrIp(string text, out IPAddress network, out IPAddress mask)
        {
            network = IPAddress.Any; mask = IPAddress.Any;

            if (text.Contains('/'))
            {
                var parts = text.Split('/');
                if (parts.Length != 2) return false;
                if (!IPAddress.TryParse(parts[0], out var baseIp)) return false;
                if (!int.TryParse(parts[1], out var prefix) || prefix < 0 || prefix > 32) return false;

                network = baseIp;
                uint m = prefix == 0 ? 0u : 0xFFFFFFFFu << (32 - prefix);
                mask = new IPAddress(BitConverter.GetBytes(m).Reverse().ToArray());
                return true;
            }
            else
            {
                if (!IPAddress.TryParse(text, out var ip)) return false;
                network = ip;
                mask = new IPAddress(new byte[] { 255, 255, 255, 255 });
                return true;
            }
        }

        private static bool IsInSubnet(IPAddress address, IPAddress network, IPAddress mask)
        {
            var a = address.GetAddressBytes();
            var n = network.GetAddressBytes();
            var m = mask.GetAddressBytes();
            if (a.Length != 4 || n.Length != 4 || m.Length != 4) return false;

            for (int i = 0; i < 4; i++)
            {
                if ((a[i] & m[i]) != (n[i] & m[i])) return false;
            }
            return true;
        }

        // Resolve where to store JSON (same place as other app data)
        private static string ResolveDataRoot()
        {
            // Adjust this to your existing pattern if different:
            // e.g., Path.Combine(SandboxHelper.GetSandboxPath(), ".system")
            var local = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "OfficeOfShadows");

            Directory.CreateDirectory(local);
            return local;
        }
    }
}
