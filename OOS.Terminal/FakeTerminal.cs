using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace OOS.Terminal
{
    public class FakeTerminal
    {
        private readonly Dictionary<string, Func<string[], string>> _commands;
        private readonly string _sandboxRoot;

        public FakeTerminal()
        {
            // sandbox folder where the game will create files (safe)
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            _sandboxRoot = Path.Combine(desktop, "OfficeOfShadows");
            Directory.CreateDirectory(_sandboxRoot);

            _commands = new Dictionary<string, Func<string[], string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["help"] = args => "Available: help, whoami, scan, list, read, quarantine, touch, clear",
                ["whoami"] = args => Environment.UserName,
                ["scan"] = args => RunFakeScan(),
                ["list"] = args => ListSandbox(),
                ["read"] = args => args.Length > 0 ? ReadFile(args[0]) : "Usage: read <filename>",
                ["quarantine"] = args => args.Length > 0 ? Quarantine(args[0]) : "Usage: quarantine <filename>",
                ["touch"] = args => args.Length > 0 ? CreateFile(args[0]) : "Usage: touch <filename>",
                ["clear"] = args => { return "\u001b[2J\u001b[H"; } // not actual terminal clear; we treat as text
            };
        }

        public string Execute(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "";
            var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var cmd = parts[0];
            var args = parts.Skip(1).ToArray();

            if (_commands.TryGetValue(cmd, out var action))
            {
                try { return action(args); }
                catch (Exception ex) { return $"Error: {ex.Message}"; }
            }
            return $"'{cmd}' is not recognized.";
        }

        private string RunFakeScan()
        {
            // Simulated scan output (multi-line)
            var sb = new StringBuilder();
            sb.AppendLine("Starting scan...");
            sb.AppendLine("Checking logs...");
            sb.AppendLine($"Found suspicious artifact: {_sandboxRoot}\\secret.log");
            sb.AppendLine("Scan complete.");
            return sb.ToString();
        }

        private string ListSandbox()
        {
            var files = Directory.GetFiles(_sandboxRoot).Select(Path.GetFileName).ToArray();
            if (files.Length == 0) return "Sandbox empty.";
            return string.Join('\n', files);
        }

        private string ReadFile(string name)
        {
            var target = Path.Combine(_sandboxRoot, name);
            if (!File.Exists(target)) return "File not found.";
            return File.ReadAllText(target);
        }

        private string CreateFile(string name)
        {
            var target = Path.Combine(_sandboxRoot, name);
            File.WriteAllText(target, $"This file was created by the game on {DateTime.Now:O}");
            return $"Created: {target}";
        }

        private string Quarantine(string name)
        {
            var src = Path.Combine(_sandboxRoot, name);
            if (!File.Exists(src)) return "File not found.";
            var q = Path.Combine(_sandboxRoot, "quarantine");
            Directory.CreateDirectory(q);
            var dst = Path.Combine(q, Path.GetFileName(src));
            File.Move(src, dst);
            return $"Quarantined: {dst}";
        }
    }
}
