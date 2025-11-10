using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace OOS.Shared
{
    public static class IntegrityManager
    {
        /// <summary>
        /// Runs startup integrity/repair and returns a human-readable report.
        /// - Ensures sandbox exists and seeds README.txt.
        /// - Parses ./Assets/manifest.json, creating dirs/files and SPECIAL-CASE creating .lnk files.
        /// - Mirrors any missing files from ./Assets/SandboxSeed/.
        /// </summary>
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

                // Always ensure README for immersion
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

                // ---- MANIFEST PROCESSING ----
                var exeDir = AppContext.BaseDirectory;
                if (!string.IsNullOrWhiteSpace(manifestPath) && File.Exists(manifestPath))
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
                    var root = doc.RootElement;

                    // Common array property names we’ll accept
                    string[] listNames = { "files", "items", "entries" };
                    if (TryGetFirstArray(root, listNames, out var list))
                    {
                        foreach (var el in list.EnumerateArray())
                        {
                            string? rel = TryGetString(el, "path")
                                          ?? TryGetString(el, "relativePath")
                                          ?? TryGetString(el, "target")
                                          ?? TryGetString(el, "to");

                            bool required = TryGetBool(el, "required") ?? false;
                            string? type = TryGetString(el, "type");

                            if (string.IsNullOrWhiteSpace(rel))
                                continue;

                            // Normalize separators
                            rel = rel.Replace('/', Path.DirectorySeparatorChar)
                                     .Replace('\\', Path.DirectorySeparatorChar);

                            checkedCount++;

                            var targetPath = Path.Combine(sandboxPath, rel);

                            // Determine if entry is a directory.
                            bool looksDir =
                                rel.EndsWith(Path.DirectorySeparatorChar) ||
                                (type?.Equals("dir", StringComparison.OrdinalIgnoreCase) ?? false) ||
                                (TryGetBool(el, "isDir") ?? false) ||
                                // NEW: treat plain names with no extension as directories (e.g., "Notes")
                                !Path.HasExtension(rel);

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

                            // Special-case shortcuts (.lnk)
                            if (rel.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                            {
                                HandleShortcut(rel, sandboxPath, exeDir, required, lines, ref repairedCount, ref warnings);
                                continue;
                            }

                            // Regular file: copy from ./Assets/<rel> if missing
                            var sourcePath = Path.Combine(exeDir, "Assets", rel);
                            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

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

                // ---- SandboxSeed mirror (optional) ----
                var seedDir = Path.Combine(AppContext.BaseDirectory, "Assets", "SandboxSeed");
                if (Directory.Exists(seedDir))
                {
                    foreach (var src in Directory.GetFiles(seedDir, "*", SearchOption.AllDirectories))
                    {
                        var rel = Path.GetRelativePath(seedDir, src);
                        var dst = Path.Combine(sandboxPath, rel);
                        if (!File.Exists(dst))
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
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
            Directory.CreateDirectory(Path.GetDirectoryName(linkPath)!);

            // If the shortcut already exists, do nothing (fixes "Created shortcut" every run).
            if (File.Exists(linkPath))
            {
                lines.Add($"Shortcut OK: {relativeLinkName}");
                return;
            }

            // Decide what the link should point to
            string lower = relativeLinkName.ToLowerInvariant();

            if (lower == "terminal.lnk")
            {
                string? terminalExe = FindTerminalExe(exeDir);
                if (terminalExe != null)
                {
                    if (CreateWindowsShortcut(linkPath, terminalExe, Path.GetDirectoryName(terminalExe)!))
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
            else if (lower == "vpn.lnk" || lower == "email.lnk")
            {
                // Optional shortcuts (by design). Only warn if required=true
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
                // Unknown .lnk from manifest — copy if present in Assets
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

        private static string? FindTerminalExe(string exeDir)
        {
            // 1) Same folder as the game
            var probe1 = Path.Combine(exeDir, "OOS.Terminal.exe");
            if (File.Exists(probe1)) return probe1;

            // 2) Common dev layout: sibling project bin
            try
            {
                var root = Directory.GetParent(exeDir)?.Parent?.Parent?.Parent?.FullName; // heuristic
                if (!string.IsNullOrEmpty(root))
                {
                    var candidates = Directory.GetFiles(root, "OOS.Terminal.exe", SearchOption.AllDirectories);
                    foreach (var c in candidates)
                    {
                        if (c.Contains("net", StringComparison.OrdinalIgnoreCase))
                            return c;
                    }
                    if (candidates.Length > 0) return candidates[0];
                }
            }
            catch { /* best-effort */ }

            // 3) Local recursive search
            try
            {
                var candidates = Directory.GetFiles(exeDir, "OOS.Terminal.exe", SearchOption.AllDirectories);
                if (candidates.Length > 0) return candidates[0];
            }
            catch { }

            return null;
        }

        /// <summary>
        /// Create a .lnk using late-bound Windows Script Host (no compile-time COM reference needed).
        /// Sets icon to the standard cmd.exe icon.
        /// </summary>
        private static bool CreateWindowsShortcut(string linkPath, string targetExe, string workingDir)
        {
            try
            {
                var shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return false;

                dynamic shell = Activator.CreateInstance(shellType)!;
                dynamic shortcut = shell.CreateShortcut(linkPath);
                shortcut.TargetPath = targetExe;
                shortcut.WorkingDirectory = workingDir;
                shortcut.WindowStyle = 1; // normal window

                // Use the cmd.exe icon for the terminal shortcut
                var system32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
                var cmdIcon = Path.Combine(system32, "cmd.exe");
                shortcut.IconLocation = cmdIcon + ",0";

                shortcut.Description = "Office of Shadows Terminal";
                shortcut.Save();
                return File.Exists(linkPath);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// If .lnk creation isn't possible, write a .url Internet shortcut as a fallback.
        /// Explorer treats it as a clickable link to the local exe.
        /// </summary>
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
            catch
            {
                return false;
            }
        }

        // ---------- JSON helpers ----------

        private static bool TryGetFirstArray(JsonElement obj, string[] names, out JsonElement array)
        {
            foreach (var n in names)
            {
                if (obj.TryGetProperty(n, out var el) && el.ValueKind == JsonValueKind.Array)
                {
                    array = el;
                    return true;
                }
            }
            array = default;
            return false;
        }

        private static string? TryGetString(JsonElement el, string name)
            => el.ValueKind == JsonValueKind.Object
               && el.TryGetProperty(name, out var v)
               && v.ValueKind == JsonValueKind.String
                   ? v.GetString()
                   : null;

        private static bool? TryGetBool(JsonElement el, string name)
        {
            if (el.ValueKind != JsonValueKind.Object || !el.TryGetProperty(name, out var v)) return null;
            if (v.ValueKind == JsonValueKind.True) return true;
            if (v.ValueKind == JsonValueKind.False) return false;
            return null;
        }
    }
}
