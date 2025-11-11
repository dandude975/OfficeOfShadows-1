using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using System.Management; // NuGet: System.Management

namespace OOS.DeviceManager
{
    public partial class DeviceManagerWindow : Window
    {
        private readonly DispatcherTimer _networkTimer;

        public DeviceManagerWindow()
        {
            InitializeComponent();

            // Hardware (already curated in your last build)
            Dispatcher.BeginInvoke(new Action(PopulateHardwareList), DispatcherPriority.Background);

            // Network
            PopulateNetwork();
            _networkTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _networkTimer.Tick += (s, e) => { if (AutoRefreshChk.IsChecked == true) PopulateNetwork(); };
            _networkTimer.Start();
        }

        // window chrome
        private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        { try { DragMove(); } catch { } }
        private void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();

        // ========================= HARDWARE (unchanged from your curated set) =========================
        private void PopulateHardwareList()
        {
            var devices = new List<HardwareDevice>();

            SafeAdd(devices, QueryRam);
            SafeAdd(devices, QueryDisks);
            SafeAdd(devices, QueryAudioDevices);
            SafeAdd(devices, QueryUsbExternalDevices);
            SafeAdd(devices, QueryGpus);
            SafeAdd(devices, QueryMotherboardSingle);
            SafeAdd(devices, QueryExternalMonitors);

            devices = devices.OrderBy(d => d.DeviceType).ThenBy(d => d.Name).ToList();
            HardwareGrid.ItemsSource = devices;

            if (devices.Count == 0)
            {
                HardwareGrid.ItemsSource = new[]
                {
                    new HardwareDevice("No devices returned", "Info", "—", "WMI may be disabled/inaccessible. Ensure the 'Windows Management Instrumentation' service is running and try x64 build.")
                };
            }
        }

        private static void SafeAdd(List<HardwareDevice> list, Func<IEnumerable<HardwareDevice>> producer)
        {
            try { var items = producer(); if (items != null) list.AddRange(items); }
            catch { /* keep partial results */ }
        }

        // ========================= NETWORK =========================

        private void PopulateNetwork()
        {
            try
            {
                // Interfaces
                var interfaces = new List<InterfaceModel>();
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    var props = ni.GetIPProperties();
                    var addrs = props.UnicastAddresses
                                     .Select(u => u.Address)
                                     .Where(a => a != null)
                                     .Select(a => a.ToString())
                                     .Where(a => !string.IsNullOrWhiteSpace(a));
                    interfaces.Add(new InterfaceModel
                    {
                        Name = ni.Name,
                        OperationalStatus = ni.OperationalStatus.ToString(),
                        Addresses = string.Join(", ", addrs)
                    });
                }
                InterfaceGrid.ItemsSource = interfaces;

                // Active TCP connections with split IP/Port columns
                var ips = IPGlobalProperties.GetIPGlobalProperties();
                var conns = ips.GetActiveTcpConnections()
                               .Select(c => new ConnectionModel
                               {
                                   LocalIP = GetAddressOnly(c.LocalEndPoint),
                                   LocalPort = GetPortOnly(c.LocalEndPoint),
                                   RemoteIP = GetAddressOnly(c.RemoteEndPoint),
                                   RemotePort = GetPortOnly(c.RemoteEndPoint),
                                   State = c.State.ToString()
                               })
                               .OrderBy(c => c.RemoteIP).ThenBy(c => c.RemotePort)
                               .ToList();
                ConnectionsGrid.ItemsSource = conns;

                // Router info (LAN from default gateway; WAN via public IP lookup)
                var lan = GetDefaultGatewayIPv4();
                RouterLanText.Text = string.IsNullOrWhiteSpace(lan) ? "Unknown" : lan;
                _ = PopulateRouterWanAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Network enumeration failed: " + ex.Message, "Device Manager",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private static string GetAddressOnly(System.Net.EndPoint ep)
        {
            if (ep is System.Net.IPEndPoint ipep) return ipep.Address.ToString();
            return "";
        }

        private static string GetPortOnly(System.Net.EndPoint ep)
        {
            if (ep is System.Net.IPEndPoint ipep) return ipep.Port.ToString();
            return "";
        }

        private static string GetDefaultGatewayIPv4()
        {
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces().Where(n => n.OperationalStatus == OperationalStatus.Up))
                {
                    var gw = ni.GetIPProperties().GatewayAddresses
                               .Select(g => g?.Address)
                               .FirstOrDefault(a => a != null && a.AddressFamily == AddressFamily.InterNetwork);
                    if (gw != null) return gw.ToString();
                }
            }
            catch { }
            return "";
        }

        private async Task PopulateRouterWanAsync()
        {
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
                // simple, plaintext service
                var text = await http.GetStringAsync("https://api.ipify.org");
                var ip = text?.Trim();
                if (!string.IsNullOrWhiteSpace(ip))
                    RouterWanText.Text = ip;
                else
                    RouterWanText.Text = "Unknown";
            }
            catch
            {
                RouterWanText.Text = "Unknown";
            }
        }

        // --- Legacy event hooks for XAML ---
        private void HardwareGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // Optional: display basic info on select
            if (HardwareGrid.SelectedItem is HardwareDevice dev)
            {
                DetailName.Text = $"Name:  {dev.Name}";
                DetailType.Text = $"Type:  {dev.DeviceType}";
                DetailStatus.Text = $"State: {dev.Status}";
                DetailNotes.Text = dev.Details;
            }
        }

        private void RefreshHardwareBtn_Click(object sender, RoutedEventArgs e)
        {
            PopulateHardwareList();
        }

        private void RevealBtn_Click(object sender, RoutedEventArgs e)
        {
            if (HardwareGrid.SelectedItem is HardwareDevice dev)
                MessageBox.Show($"{dev.Name}\n\n{dev.DeviceType}\n\n{dev.Details}",
                    "Device details", MessageBoxButton.OK, MessageBoxImage.Information);
        }


        private void RefreshNetworkBtn_Click(object sender, RoutedEventArgs e) => PopulateNetwork();

        // ========================= WMI (hardware) =========================

        private static ManagementObjectCollection Query(string wql, string scopePath = @"\\.\root\cimv2")
        {
            var scope = new ManagementScope(scopePath);
            scope.Connect();
            return new ManagementObjectSearcher(scope, new ObjectQuery(wql)).Get();
        }

        // RAM
        private static IEnumerable<HardwareDevice> QueryRam()
        {
            var list = new List<HardwareDevice>();
            foreach (ManagementObject mo in Query("SELECT * FROM Win32_PhysicalMemory"))
            {
                string cap = TryFormatBytes(mo["Capacity"]?.ToString());
                var mfg = (mo["Manufacturer"] as string);
                var part = (mo["PartNumber"] as string);
                var speed = mo["Speed"]?.ToString();
                var bank = (mo["BankLabel"] as string);

                var name = $"{cap} RAM".Trim();
                var sb = new StringBuilder();
                if (!string.IsNullOrWhiteSpace(mfg)) sb.AppendLine($"Manufacturer: {mfg}");
                if (!string.IsNullOrWhiteSpace(part)) sb.AppendLine($"Part: {part}");
                if (!string.IsNullOrWhiteSpace(speed)) sb.AppendLine($"Speed: {speed} MHz");
                if (!string.IsNullOrWhiteSpace(bank)) sb.AppendLine($"Bank: {bank}");

                list.Add(new HardwareDevice(name, "RAM", "OK", sb.ToString().Trim()));
            }
            return list;
        }

        // Storage
        private static IEnumerable<HardwareDevice> QueryDisks()
        {
            var list = new List<HardwareDevice>();
            foreach (ManagementObject mo in Query("SELECT * FROM Win32_DiskDrive"))
            {
                var model = (mo["Model"] as string) ?? "Disk";
                var iface = (mo["InterfaceType"] as string);
                var size = TryFormatBytes(mo["Size"]?.ToString());
                var media = (mo["MediaType"] as string);
                var busType = (mo["PNPDeviceID"] as string)?.Contains("NVMe", StringComparison.OrdinalIgnoreCase) == true ? "NVMe" : iface;

                var sb = new StringBuilder();
                if (!string.IsNullOrWhiteSpace(busType)) sb.AppendLine($"Bus: {busType}");
                if (!string.IsNullOrWhiteSpace(media)) sb.AppendLine($"Media: {media}");
                if (!string.IsNullOrWhiteSpace(size)) sb.AppendLine($"Size: {size}");

                list.Add(new HardwareDevice(model, "Storage", "OK", sb.ToString().Trim()));
            }
            return list;
        }

        // Audio
        private static IEnumerable<HardwareDevice> QueryAudioDevices()
        {
            var list = new List<HardwareDevice>();
            foreach (ManagementObject mo in Query("SELECT * FROM Win32_SoundDevice"))
            {
                var name = (mo["Name"] as string) ?? "Audio Device";
                var mfg = (mo["Manufacturer"] as string);
                var product = (mo["ProductName"] as string);
                var status = (mo["Status"] as string) ?? "OK";

                var sb = new StringBuilder();
                if (!string.IsNullOrWhiteSpace(mfg)) sb.AppendLine($"Manufacturer: {mfg}");
                if (!string.IsNullOrWhiteSpace(product)) sb.AppendLine($"Product: {product}");

                list.Add(new HardwareDevice(name, "Audio", status, sb.ToString().Trim()));
            }
            return list;
        }

        // External USB
        private static IEnumerable<HardwareDevice> QueryUsbExternalDevices()
        {
            var list = new List<HardwareDevice>();
            foreach (ManagementObject mo in Query("SELECT * FROM Win32_PnPEntity"))
            {
                var id = (mo["DeviceID"] as string);
                if (string.IsNullOrWhiteSpace(id) || !id.StartsWith("USB\\", StringComparison.OrdinalIgnoreCase))
                    continue;

                var name = (mo["Name"] as string) ?? "USB Device";
                var className = (mo["PNPClass"] as string) ?? "";

                if (name.Contains("Host Controller", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Root Hub", StringComparison.OrdinalIgnoreCase) ||
                    (className.Equals("USB", StringComparison.OrdinalIgnoreCase) && name.Contains("Hub", StringComparison.OrdinalIgnoreCase)))
                    continue;

                var mfg = (mo["Manufacturer"] as string);
                var status = (mo["Status"] as string) ?? "OK";

                string vid = "", pid = "";
                var parts = id.Split('\\');
                if (parts.Length > 1)
                {
                    foreach (var seg in parts[1].Split('&'))
                    {
                        if (seg.StartsWith("VID_", StringComparison.OrdinalIgnoreCase)) vid = seg[4..];
                        if (seg.StartsWith("PID_", StringComparison.OrdinalIgnoreCase)) pid = seg[4..];
                    }
                }

                var sb = new StringBuilder();
                if (!string.IsNullOrWhiteSpace(mfg)) sb.AppendLine($"Manufacturer: {mfg}");
                if (!string.IsNullOrWhiteSpace(vid)) sb.AppendLine($"VID: {vid}");
                if (!string.IsNullOrWhiteSpace(pid)) sb.AppendLine($"PID: {pid}");
                sb.AppendLine($"PNPDeviceID: {id}");

                list.Add(new HardwareDevice(name, "USB (External)", status, sb.ToString().Trim()));
            }
            return list;
        }

        // GPU
        private static IEnumerable<HardwareDevice> QueryGpus()
        {
            var list = new List<HardwareDevice>();
            foreach (ManagementObject mo in Query("SELECT * FROM Win32_VideoController"))
            {
                var name = (mo["Name"] as string) ?? "GPU";
                var drv = (mo["DriverVersion"] as string);
                var vram = TryFormatBytes(mo["AdapterRAM"]?.ToString());
                var sb = new StringBuilder();
                if (!string.IsNullOrWhiteSpace(vram)) sb.AppendLine($"VRAM: {vram}");
                if (!string.IsNullOrWhiteSpace(drv)) sb.AppendLine($"Driver: {drv}");
                list.Add(new HardwareDevice(name, "GPU", "OK", sb.ToString().Trim()));
            }
            return list;
        }

        // Motherboard (single)
        private static IEnumerable<HardwareDevice> QueryMotherboardSingle()
        {
            var boards = new List<(string mfg, string prod, string ver)>();
            foreach (ManagementObject mo in Query("SELECT * FROM Win32_BaseBoard"))
            {
                boards.Add(((mo["Manufacturer"] as string) ?? "", (mo["Product"] as string) ?? "", (mo["Version"] as string) ?? ""));
            }

            if (boards.Count == 0) yield break;

            var mfg = string.Join(" / ", boards.Select(b => b.mfg).Distinct().Where(s => !string.IsNullOrWhiteSpace(s)));
            var prod = string.Join(" / ", boards.Select(b => b.prod).Distinct().Where(s => !string.IsNullOrWhiteSpace(s)));
            var ver = string.Join(" / ", boards.Select(b => b.ver).Distinct().Where(s => !string.IsNullOrWhiteSpace(s)));

            var name = $"{mfg} {prod}".Trim();
            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(mfg)) sb.AppendLine($"Manufacturer: {mfg}");
            if (!string.IsNullOrWhiteSpace(prod)) sb.AppendLine($"Product: {prod}");
            if (!string.IsNullOrWhiteSpace(ver)) sb.AppendLine($"Version: {ver}");

            yield return new HardwareDevice(string.IsNullOrWhiteSpace(name) ? "Motherboard" : name, "Motherboard", "OK", sb.ToString().Trim());
        }

        // External monitors
        private static IEnumerable<HardwareDevice> QueryExternalMonitors()
        {
            var list = new List<HardwareDevice>();
            try
            {
                foreach (ManagementObject mo in Query("SELECT * FROM WmiMonitorID", @"\\.\root\wmi"))
                {
                    string vendor = DecodeUshortArray(mo["ManufacturerName"] as ushort[]);
                    string product = DecodeUshortArray(mo["ProductCodeID"] as ushort[]);
                    string serial = DecodeUshortArray(mo["SerialNumberID"] as ushort[]);
                    string user = DecodeUshortArray(mo["UserFriendlyName"] as ushort[]);

                    var name = !string.IsNullOrWhiteSpace(user) ? user : (!string.IsNullOrWhiteSpace(product) ? product : "Monitor");
                    var sb = new StringBuilder();
                    if (!string.IsNullOrWhiteSpace(vendor)) sb.AppendLine($"Manufacturer: {vendor}");
                    if (!string.IsNullOrWhiteSpace(user)) sb.AppendLine($"Model: {user}");
                    if (!string.IsNullOrWhiteSpace(product)) sb.AppendLine($"Product: {product}");
                    if (!string.IsNullOrWhiteSpace(serial)) sb.AppendLine($"Serial: {serial}");

                    list.Add(new HardwareDevice(name, "Monitor", "OK", sb.ToString().Trim()));
                }
            }
            catch { }

            if (list.Count == 0)
            {
                try
                {
                    foreach (ManagementObject mo in Query("SELECT * FROM Win32_DesktopMonitor"))
                    {
                        var name = (mo["Name"] as string) ?? "Monitor";
                        var mfg = (mo["MonitorManufacturer"] as string);
                        var id = (mo["PNPDeviceID"] as string);
                        var sb = new StringBuilder();
                        if (!string.IsNullOrWhiteSpace(mfg)) sb.AppendLine($"Manufacturer: {mfg}");
                        if (!string.IsNullOrWhiteSpace(id)) sb.AppendLine($"PNPDeviceID: {id}");
                        list.Add(new HardwareDevice(name, "Monitor", "OK", sb.ToString().Trim()));
                    }
                }
                catch { }
            }

            return list;
        }

        // ========================= UTIL =========================

        private static string DecodeUshortArray(ushort[]? data)
        {
            if (data == null) return string.Empty;
            var chars = data.TakeWhile(v => v != 0).Select(v => (char)v).ToArray();
            return new string(chars);
        }

        private static string TryFormatBytes(string? raw)
        {
            if (raw == null || !ulong.TryParse(raw, out var v)) return "";
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            int i = 0; double d = v;
            while (d >= 1024 && i < units.Length - 1) { d /= 1024; i++; }
            return $"{d:0.##} {units[i]}";
        }

        private static string TryFormatBitsPerSec(string? raw)
        {
            if (raw == null || !ulong.TryParse(raw, out var v)) return "";
            string[] units = { "bps", "Kbps", "Mbps", "Gbps" };
            int i = 0; double d = v;
            while (d >= 1000 && i < units.Length - 1) { d /= 1000; i++; }
            return $"{d:0.##} {units[i]}";
        }
    }

    // Models
    public sealed class HardwareDevice
    {
        public HardwareDevice(string name, string type, string status, string details)
        { Name = name; DeviceType = type; Status = status; Details = details; }

        public string Name { get; }
        public string DeviceType { get; }
        public string Status { get; }
        public string Details { get; }
    }

    public class InterfaceModel
    {
        public string Name { get; set; } = "";
        public string OperationalStatus { get; set; } = "";
        public string Addresses { get; set; } = "";
    }

    public class ConnectionModel
    {
        public string LocalIP { get; set; } = "";
        public string LocalPort { get; set; } = "";
        public string RemoteIP { get; set; } = "";
        public string RemotePort { get; set; } = "";
        public string State { get; set; } = "";
    }
}


