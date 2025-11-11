using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace OOS.Game
{
    public sealed class IntegrityManager
    {
        public Task RunStartupCheck(string sandboxPath, string installRoot, Action<string>? log = null)
            => RunAllChecksAsync(sandboxPath, installRoot, log);

        public async Task RunAllChecksAsync(string sandboxPath, string installRoot, Action<string>? log = null)
        {
            log ??= _ => { };

            try
            {
                EnsureDirectory(sandboxPath, log, "Sandbox root");

                var manifest = LoadManifestFlexible(installRoot, log);

                log($"[Integrity] Processing {manifest.Entries.Count} entries…");

                for (int i = 0; i < manifest.Entries.Count; i++)
                {
                    var entry = manifest.Entries[i];
                    try
                    {
                        log($"[Integrity] [{i + 1}/{manifest.Entries.Count}] {entry.Kind} -> {entry.Path}");
                        var targetPath = Path.Combine(sandboxPath, entry.Path.NormalizeSlashes());

                        switch (entry.Kind?.Trim().ToLowerInvariant())
                        {
                            case "folder":
                            case "directory":
                                EnsureDirectory(targetPath, log, entry.Path);
                                break;

                            case "file":
                                await EnsureFileAsync(targetPath, installRoot, entry, log);
                                break;

                            case "shortcut":
                                await EnsureShortcutAsync(targetPath, installRoot, entry, log);
                                break;

                            default:
                                log($"[Integrity] Unknown kind \"{entry.Kind}\" for \"{entry.Path}\" – skipping.");
                                break;
                        }
                    }
                    catch (Exception exEntry)
                    {
                        log($"[Integrity] Entry failed ({entry.Kind} {entry.Path}): {exEntry.Message}");
                        // continue with next entry
                    }
                }

                // Safety: always ensure Notes exists
                EnsureDirectory(Path.Combine(sandboxPath, "Notes"), log, "Notes");

                // Helpful README
                await EnsureReadmeAsync(sandboxPath, log);

                log("[Integrity] Completed.");
            }
            catch (Exception ex)
            {
                log($"[Integrity] Fatal error: {ex}");
                // Swallow to prevent app exit
            }
        }

        private sealed class Manifest
        {
            public List<ManifestEntry> Entries { get; set; } = new();
        }

        private sealed class ManifestEntry
        {
            public string Path { get; set; } = "";
            public string Kind { get; set; } = "";
            public string? Source { get; set; }
            public bool Required { get; set; } = false;
            public string? Icon { get; set; }
            public int IconIndex { get; set; } = 0;
            public string? Arguments { get; set; }
        }

        private static void EnsureDirectory(string dir, Action<string> log, string labelForLog)
        {
            try
            {
                if (Directory.Exists(dir))
                    log($"[Integrity] Folder OK: {labelForLog}");
                else
                {
                    Directory.CreateDirectory(dir);
                    log($"[Integrity] Created folder: {labelForLog}");
                }
            }
            catch (Exception ex)
            {
                log($"[Integrity] Failed to create folder \"{labelForLog}\": {ex.Message}");
            }
        }

        private async Task EnsureFileAsync(string targetPath, string installRoot, ManifestEntry entry, Action<string> log)
        {
            try
            {
                if (File.Exists(targetPath))
                {
                    log($"[Integrity] File OK: {entry.Path}");
                    return;
                }

                string? src = ResolvePathMaybe(installRoot, entry.Source);
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

                if (!string.IsNullOrWhiteSpace(src) && File.Exists(src))
                {
                    File.Copy(src, targetPath, overwrite: false);
                    log($"[Integrity] Seeded file: {entry.Path}");
                }
                else
                {
                    await File.WriteAllTextAsync(targetPath, "", Encoding.UTF8);
                    log($"[Integrity] Created placeholder file: {entry.Path}");
                }
            }
            catch (Exception ex)
            {
                log($"[Integrity] Failed to create file \"{entry.Path}\": {ex.Message}");
            }
        }

        private async Task EnsureShortcutAsync(string lnkPath, string installRoot, ManifestEntry entry, Action<string> log)
        {
            try
            {
                if (File.Exists(lnkPath))
                {
                    log($"[Integrity] Shortcut OK: {Path.GetFileName(lnkPath)}");
                    return;
                }

                string? explicitTarget = ResolvePathMaybe(installRoot, entry.Source);
                string? targetExe = explicitTarget ?? AutoDetectTargetExeFromShortcut(installRoot, lnkPath, log);

                if (string.IsNullOrWhiteSpace(targetExe) || !File.Exists(targetExe))
                {
                    log($"[Integrity] Could not locate target EXE for \"{Path.GetFileName(lnkPath)}\" – skipping.");
                    return;
                }

                string fileName = Path.GetFileName(lnkPath);
                string? iconPath = ResolvePathMaybe(installRoot, entry.Icon);
                int iconIndex = entry.IconIndex;

                if (string.IsNullOrWhiteSpace(iconPath))
                {
                    if (fileName.Equals("Terminal.lnk", StringComparison.OrdinalIgnoreCase))
                    {
                        iconPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
                        if (!File.Exists(iconPath)) iconPath = targetExe;
                        iconIndex = 0;
                    }
                    else
                    {
                        iconPath = targetExe;
                        iconIndex = 0;
                    }
                }

                var created = CreateShortcut(targetExe, lnkPath, entry.Arguments ?? "", iconPath!, iconIndex);
                log(created
                    ? $"[Integrity] Created shortcut: {Path.GetFileName(lnkPath)}"
                    : $"[Integrity] Failed to create shortcut: {Path.GetFileName(lnkPath)}");

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                log($"[Integrity] Shortcut error for \"{Path.GetFileName(lnkPath)}\": {ex.Message}");
            }
        }

        private static bool CreateShortcut(string targetExe, string lnkPath, string arguments, string iconPath, int iconIndex)
        {
            try
            {
                var dir = Path.GetDirectoryName(lnkPath)!;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                var wshType = Type.GetTypeFromProgID("WScript.Shell");
                if (wshType == null)
                {
                    // Extremely rare on Windows; log and skip
                    return false;
                }

                dynamic wsh = Activator.CreateInstance(wshType)!;
                dynamic shortcut = wsh.CreateShortcut(lnkPath);

                shortcut.TargetPath = targetExe;
                shortcut.WorkingDirectory = Path.GetDirectoryName(targetExe);
                shortcut.Arguments = arguments ?? "";

                if (!string.IsNullOrWhiteSpace(iconPath) && File.Exists(iconPath))
                    shortcut.IconLocation = $"{iconPath},{iconIndex}";
                else
                    shortcut.IconLocation = targetExe;

                shortcut.Save();

                System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shortcut);
                System.Runtime.InteropServices.Marshal.FinalReleaseComObject(wsh);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string? ResolvePathMaybe(string installRoot, string? maybeRelativeOrAbsolute)
        {
            if (string.IsNullOrWhiteSpace(maybeRelativeOrAbsolute))
                return null;

            if (Path.IsPathRooted(maybeRelativeOrAbsolute))
                return maybeRelativeOrAbsolute;

            return Path.Combine(installRoot, maybeRelativeOrAbsolute.NormalizeSlashes());
        }

        private static string? AutoDetectTargetExeFromShortcut(string installRoot, string lnkPath, Action<string> log)
        {
            var name = Path.GetFileName(lnkPath) ?? "";
            var candidates = new List<string>();

            if (name.Equals("Terminal.lnk", StringComparison.OrdinalIgnoreCase))
                candidates.AddRange(new[] { "OOS.Terminal.exe", "Terminal.exe" });

            if (name.Equals("DeviceManager.lnk", StringComparison.OrdinalIgnoreCase))
                candidates.AddRange(new[] { "OOS.DeviceManager.exe", "DeviceManager.exe" });

            if (name.Equals("Firewall.lnk", StringComparison.OrdinalIgnoreCase))
                candidates.AddRange(new[] { "OOS.Firewall.exe", "Firewall.exe" });

            var stem = Path.GetFileNameWithoutExtension(name);
            if (!string.IsNullOrWhiteSpace(stem))
                candidates.Add(stem + ".exe");

            var roots = EnumerateSearchRoots(installRoot, 5).ToList();
            roots.AddRange(EnumerateSiblingProjectBinRoots(installRoot, new[] { "OOS.Terminal", "OOS.DeviceManager", "OOS.Firewall" }));

            foreach (var root in roots.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                foreach (var exeName in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    var hit = FindExeUnder(root, exeName);
                    if (!string.IsNullOrWhiteSpace(hit))
                    {
                        log($"[Integrity] Resolved {name} -> {hit}");
                        return hit;
                    }
                }
            }

            var sample = string.Join("; ", roots.Take(6));
            log($"[Integrity] Auto-detect failed for {name}. Searched roots: {sample} …");
            return null;
        }

        private static IEnumerable<string> EnumerateSearchRoots(string start, int maxAncestors)
        {
            var current = new DirectoryInfo(start);
            for (int i = 0; i <= maxAncestors && current != null; i++)
            {
                yield return current.FullName;
                current = current.Parent;
            }
        }

        private static IEnumerable<string> EnumerateSiblingProjectBinRoots(string installRoot, IEnumerable<string> projectNames)
        {
            var roots = new List<string>();
            var ancestors = EnumerateSearchRoots(installRoot, 5).ToArray();

            foreach (var anc in ancestors)
            {
                foreach (var proj in projectNames)
                {
                    var basePath = Path.Combine(anc, proj, "bin");
                    if (Directory.Exists(basePath)) roots.Add(basePath);

                    var dbg = Path.Combine(basePath, "Debug");
                    var rel = Path.Combine(basePath, "Release");
                    if (Directory.Exists(dbg)) roots.Add(dbg);
                    if (Directory.Exists(rel)) roots.Add(rel);
                }
            }
            return roots;
        }

        private static string? FindExeUnder(string rootFolder, string exeName)
        {
            try
            {
                if (!Directory.Exists(rootFolder)) return null;

                var common = new[] { ".", "bin", "bin\\Debug", "bin\\Release" };
                foreach (var rel in common)
                {
                    var dir = Path.Combine(rootFolder, rel);
                    if (!Directory.Exists(dir)) continue;

                    var fast = Directory.EnumerateFiles(dir, exeName, SearchOption.AllDirectories).FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(fast)) return fast;
                }

                return Directory.EnumerateFiles(rootFolder, exeName, SearchOption.AllDirectories).FirstOrDefault();
            }
            catch
            {
                return null;
            }
        }

        private static Manifest LoadManifestFlexible(string installRoot, Action<string> log)
        {
            var candidates = new List<string>
            {
                Path.Combine(installRoot, "manifest.json"),
                Path.Combine(installRoot, "Assets", "manifest.json")
            };

            var parents = EnumerateSearchRoots(installRoot, 4).Skip(1).ToArray();
            foreach (var p in parents)
            {
                candidates.Add(Path.Combine(p, "Assets", "manifest.json"));
                candidates.Add(Path.Combine(p, "manifest.json"));
            }

            foreach (var path in candidates)
            {
                if (!File.Exists(path)) continue;

                try
                {
                    var json = File.ReadAllText(path, Encoding.UTF8);

                    var loaded = JsonSerializer.Deserialize<Manifest>(json, new JsonSerializerOptions
                    { PropertyNameCaseInsensitive = true });

                    if (loaded?.Entries != null && loaded.Entries.Count > 0)
                    {
                        log($"[Integrity] Loaded manifest: {path}");
                        return loaded;
                    }

                    var shim = JsonSerializer.Deserialize<FoxxManifestShim>(json, new JsonSerializerOptions
                    { PropertyNameCaseInsensitive = true });

                    if (shim?.Items != null && shim.Items.Count > 0)
                    {
                        log($"[Integrity] Loaded manifest (items-schema): {path}");
                        return new Manifest { Entries = shim.Items };
                    }
                }
                catch (Exception ex)
                {
                    log($"[Integrity] Failed to parse manifest at {path}: {ex.Message}");
                }
            }

            log("[Integrity] manifest.json not found – using defaults.");
            return new Manifest
            {
                Entries = new List<ManifestEntry>
                {
                    new ManifestEntry { Path = "Notes", Kind = "Folder", Required = true },
                    new ManifestEntry { Path = "Downloads", Kind = "Folder", Required = true },
                    new ManifestEntry { Path = "Terminal.lnk", Kind = "Shortcut", Required = true },
                    new ManifestEntry { Path = "DeviceManager.lnk", Kind = "Shortcut", Required = true },
                    new ManifestEntry { Path = "Firewall.lnk", Kind = "Shortcut", Required = true },
                    new ManifestEntry { Path = "Docs\\FIREWALL_GUIDE.txt", Kind = "File", Source = "Assets\\Seed\\FIREWALL_GUIDE.txt" },
                    new ManifestEntry { Path = "Docs\\README.txt", Kind = "File", Source = "Assets\\Seed\\README.txt" }
                }
            };
        }

        private sealed class FoxxManifestShim
        {
            public string? Version { get; set; }
            public List<ManifestEntry> Items { get; set; } = new();
        }

        private static async Task EnsureReadmeAsync(string sandboxPath, Action<string> log)
        {
            try
            {
                var docsDir = Path.Combine(sandboxPath, "Docs");
                if (!Directory.Exists(docsDir)) Directory.CreateDirectory(docsDir);

                var readmePath = Path.Combine(docsDir, "README.txt");
                if (File.Exists(readmePath))
                {
                    log("[Integrity] README OK.");
                    return;
                }

                var text =
@"OFFICE OF SHADOWS :: SANDBOX
This folder emulates the Operator's desktop.
Shortcuts launch in-world apps. Files placed here may be scanned by Antivirus.
If something hostile arrives, quarantine or purge it.";
                await File.WriteAllTextAsync(readmePath, text, Encoding.UTF8);
                log("[Integrity] Seeded README.txt.");
            }
            catch (Exception ex)
            {
                log($"[Integrity] Failed to seed README: {ex.Message}");
            }
        }
    }

    internal static class StringPathExtensions
    {
        public static string NormalizeSlashes(this string path)
            => path.Replace('/', Path.DirectorySeparatorChar)
                   .Replace('\\', Path.DirectorySeparatorChar);
    }
}
