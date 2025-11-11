using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace OOS.Shared
{
    public static class IntegrityManager
    {
        public static string RunStartupCheck(string manifestPath, string sandboxPath)
        {
            var lines = new List<string>();
            int checkedCount = 0, repairedCount = 0, seededCount = 0, warnings = 0;

            try
            {
                if (string.IsNullOrWhiteSpace(sandboxPath))
                    return "Integrity: invalid sandbox path.";

                if (!Directory.Exists(sandboxPath))
                    Directory.CreateDirectory(sandboxPath);

                // README
                var readme = Path.Combine(sandboxPath, "README.txt");
                if (!File.Exists(readme))
                {
                    File.WriteAllText(readme,
@"Office of Shadows — Desktop Sandbox

This folder is a safe in-game workspace. Files here may be created,
modified, or removed by puzzles, scripts, and the in-game Terminal.

— Stay vigilant.");
                    repairedCount++;
                    lines.Add("Seeded README.txt");
                }

                // Manifest-driven items
                var exeDir = AppContext.BaseDirectory;
                if (!string.IsNullOrWhiteSpace(manifestPath) && File.Exists(manifestPath))
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
                    var root = doc.RootElement;

                    if (TryGetFirstArray(root, new[] { "files", "items", "entries" }, out var list))
                    {
                        foreach (var el in list.EnumerateArray())
                        {
                            string? rel = TryGetString(el, "path")
                                          ?? TryGetString(el, "relativePath")
                                          ?? TryGetString(el, "target")
                                          ?? TryGetString(el, "to");

                            bool required = TryGetBool(el, "required") ?? false;
                            string? kind = TryGetString(el, "kind") ?? TryGetString(el, "type");

                            if (string.IsNullOrWhiteSpace(rel))
                                continue;

                            rel = rel.Replace('/', Path.DirectorySeparatorChar)
                                     .Replace('\\', Path.DirectorySeparatorChar);

                            checkedCount++;
                            var targetPath = Path.Combine(sandboxPath, rel);

                            // Consider no-extension entries as directories (e.g., "Notes")
                            bool looksDir = rel.EndsWith(Path.DirectorySeparatorChar)
                                            || (kind?.Equals("dir", StringComparison.OrdinalIgnoreCase) ?? false)
                                            || (kind?.Equals("directory", StringComparison.OrdinalIgnoreCase) ?? false)
                                            || (TryGetBool(el, "isDir") ?? false)
                                            || !Path.HasExtension(rel);

                            if (looksDir)
                            {
                                if (!Directory.Exists(targetPath))
                                {
                                    Directory.CreateDirectory(targetPath);
                                    repairedCount++;
                                    lines.Add($"Created directory: {rel.TrimEnd(Path.DirectorySeparatorChar)}");
                                }
                                continue;
                            }

                            if (rel.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                            {
                                HandleShortcut(rel, sandboxPath, exeDir, required, lines, ref repairedCount, ref warnings);
                                continue;
                            }

                            var sourcePath = Path.Combine(exeDir, "Assets", rel);
                            var dir = Path.GetDirectoryName(targetPath);
                            if (!string.IsNullOrEmpty(dir))
                                Directory.CreateDirectory(dir);

                            if (!File.Exists(targetPath))
                            {
                                if (File.Exists(sourcePath))
                                {
                                    File.Copy(sourcePath, targetPath, overwrite: false);
                                    repairedCount++;
                                    lines.Add($"Restored file: {rel}");
                                }
                                else
                                {
                                    warnings++;
                                    lines.Add($"Missing source for: {rel}");
                                }
                            }
                        }
                    }
                    else
                    {
                        lines.Add("Manifest present but contains no 'files'/'items'/'entries' array.");
                    }
                }
                else
                {
                    lines.Add("Manifest not found; skipping manifest-driven repair.");
                }

                // Optional seed mirror
                var seedDir = Path.Combine(AppContext.BaseDirectory, "Assets", "SandboxSeed");
                if (Directory.Exists(seedDir))
                {
                    foreach (var src in Directory.GetFiles(seedDir, "*", SearchOption.AllDirectories))
                    {
                        var rel = Path.GetRelativePath(seedDir, src);
                        var dst = Path.Combine(sandboxPath, rel);
                        var dir = Path.GetDirectoryName(dst);
                        if (!string.IsNullOrEmpty(dir))
                            Directory.CreateDirectory(dir);

                        if (!File.Exists(dst))
                        {
                            File.Copy(src, dst, overwrite: false);
                            seededCount++;
                            lines.Add($"Seeded from SandboxSeed: {rel}");
                        }
                    }
                }

                lines.Insert(0, $"Checked: {checkedCount} | Repaired: {repairedCount} | Seeded: {seededCount}");
            }
            catch (Exception ex)
            {
                var msg = "IntegrityManager.RunStartupCheck exception:\n" + ex;
                SharedLogger.Error(msg);
                lines.Add(msg);
            }

            return string.Join(Environment.NewLine, lines);
        }

        // ---------- Shortcuts ----------

        private static void HandleShortcut(
            string relativeLinkName,
            string sandboxPath,
            string exeDir,
            bool required,
            List<string> lines,
            ref int repairedCount,
            ref int warnings)
        {
            string linkPath = Path.Combine(sandboxPath, relativeLinkName);
            var linkDir = Path.GetDirectoryName(linkPath);
            if (!string.IsNullOrEmpty(linkDir))
                Directory.CreateDirectory(linkDir);

            if (File.Exists(linkPath))
            {
                lines.Add($"Shortcut OK: {relativeLinkName}");
                return;
            }

            string lower = relativeLinkName.ToLowerInvariant();

            if (lower == "terminal.lnk")
            {
                string? terminalExe = FindExe(exeDir, "OOS.Terminal.exe");
                if (terminalExe != null)
                {
                    var system32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
                    var cmdIcon = Path.Combine(system32, "cmd.exe");

                    if (CreateWindowsShortcut(linkPath, terminalExe, Path.GetDirectoryName(terminalExe)!, cmdIcon, 0))
                    {
                        repairedCount++;
                        lines.Add("Created shortcut: Terminal.lnk");
                    }
                    else if (CreateUrlShortcutFallback(linkPath, terminalExe))
                    {
                        repairedCount++;
                        lines.Add("Created fallback shortcut (.url): Terminal.lnk → OOS.Terminal.exe");
                    }
                    else
                    {
                        warnings++;
                        lines.Add("Failed to create Terminal.lnk (no WSH and .url fallback failed).");
                    }
                }
                else
                {
                    warnings++;
                    lines.Add("Cannot locate OOS.Terminal.exe to create Terminal.lnk");
                }
            }
            else if (lower == "devicemanager.lnk")
            {
                string? dmExe = FindExe(exeDir, "OOS.DeviceManager.exe",
                    Path.Combine(exeDir, "Tools", "OOS.DeviceManager.exe"),
                    Path.Combine(exeDir, "Assets", "Tools", "OOS.DeviceManager.exe"),
                    Path.Combine(exeDir, "Assets", "OOS.DeviceManager.exe")
                );

                if (dmExe != null)
                {
                    if (CreateWindowsShortcut(linkPath, dmExe, Path.GetDirectoryName(dmExe)!, dmExe, 0))
                    {
                        repairedCount++;
                        lines.Add("Created shortcut: DeviceManager.lnk");
                    }
                    else if (CreateUrlShortcutFallback(linkPath, dmExe))
                    {
                        repairedCount++;
                        lines.Add("Created fallback shortcut (.url): DeviceManager.lnk → OOS.DeviceManager.exe");
                    }
                    else
                    {
                        warnings++;
                        lines.Add("Failed to create DeviceManager.lnk (no WSH and .url fallback failed).");
                    }
                }
                else
                {
                    warnings++;
                    lines.Add("Cannot locate OOS.DeviceManager.exe to create DeviceManager.lnk");
                }
            }
            else if (lower == "vpn.lnk" || lower == "email.lnk")
            {
                if (required)
                {
                    warnings++;
                    lines.Add($"Missing source for: {relativeLinkName} (marked required)");
                }
                else
                {
                    lines.Add($"Optional shortcut skipped: {relativeLinkName}");
                }
            }
            else
            {
                var assetsSource = Path.Combine(exeDir, "Assets", relativeLinkName);
                if (File.Exists(assetsSource))
                {
                    File.Copy(assetsSource, linkPath, overwrite: true);
                    repairedCount++;
                    lines.Add($"Restored shortcut from Assets: {relativeLinkName}");
                }
                else
                {
                    warnings++;
                    lines.Add($"Missing source for: {relativeLinkName}");
                }
            }
        }

        // ---------- Exe finder (robust) ----------

        private static string? FindExe(string exeDir, string exeName, params string[] preferredAbsolutePaths)
        {
            // 0) explicit preferred paths
            foreach (var p in preferredAbsolutePaths)
            {
                if (!string.IsNullOrWhiteSpace(p) && File.Exists(p))
                    return p;
            }

            // 1) same folder
            var p1 = Path.Combine(exeDir, exeName);
            if (File.Exists(p1)) return p1;

            // 2) typical tidy locations
            var p2 = Path.Combine(exeDir, "Tools", exeName);
            if (File.Exists(p2)) return p2;

            var p3 = Path.Combine(exeDir, "Assets", "Tools", exeName);
            if (File.Exists(p3)) return p3;

            var p4 = Path.Combine(exeDir, "Assets", exeName);
            if (File.Exists(p4)) return p4;

            // 3) walk up to solution root-ish (6 levels), search all subdirs for the exact exe name
            try
            {
                var cursor = new DirectoryInfo(exeDir);
                for (int i = 0; i < 6 && cursor?.Parent != null; i++, cursor = cursor.Parent)
                {
                    foreach (var f in Directory.EnumerateFiles(cursor.FullName, exeName, SearchOption.AllDirectories))
                    {
                        // prefer net* builds if multiple are found
                        if (f.IndexOf("net", StringComparison.OrdinalIgnoreCase) >= 0)
                            return f;
                        return f;
                    }
                }
            }
            catch { /* best effort */ }

            return null;
        }

        // ---------- Shortcut helpers ----------

        private static bool CreateWindowsShortcut(string linkPath, string targetExe, string workingDir, string? iconOverridePath, int iconIndex)
        {
            try
            {
                var shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return false;

                dynamic shell = Activator.CreateInstance(shellType)!;
                dynamic shortcut = shell.CreateShortcut(linkPath);
                shortcut.TargetPath = targetExe;
                shortcut.WorkingDirectory = workingDir;
                shortcut.WindowStyle = 1;

                if (!string.IsNullOrWhiteSpace(iconOverridePath))
                    shortcut.IconLocation = iconOverridePath + "," + iconIndex;

                shortcut.Description = Path.GetFileNameWithoutExtension(targetExe);
                shortcut.Save();
                return File.Exists(linkPath);
            }
            catch { return false; }
        }

        private static bool CreateUrlShortcutFallback(string linkPath, string targetExe)
        {
            try
            {
                var urlPath = Path.ChangeExtension(linkPath, ".url");
                var uri = new Uri(targetExe);
                File.WriteAllText(urlPath,
                    $"[InternetShortcut]{Environment.NewLine}URL=file:///{uri.LocalPath.Replace('\\', '/')}{Environment.NewLine}");
                return File.Exists(urlPath);
            }
            catch { return false; }
        }

        // ---------- JSON helpers ----------

        private static bool TryGetFirstArray(JsonElement obj, string[] names, out JsonElement array)
        {
            foreach (var n in names)
            {
                if (obj.TryGetProperty(n, out var el) && el.ValueKind == JsonValueKind.Array)
                {
                    array = el; return true;
                }
            }
            array = default; return false;
        }

        private static string? TryGetString(JsonElement el, string name)
        {
            if (el.ValueKind != JsonValueKind.Object) return null;
            if (!el.TryGetProperty(name, out var v)) return null;
            return v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        }

        private static bool? TryGetBool(JsonElement el, string name)
        {
            if (el.ValueKind != JsonValueKind.Object) return null;
            if (!el.TryGetProperty(name, out var v)) return null;
            if (v.ValueKind == JsonValueKind.True) return true;
            if (v.ValueKind == JsonValueKind.False) return false;
            return null;
        }
    }
}
