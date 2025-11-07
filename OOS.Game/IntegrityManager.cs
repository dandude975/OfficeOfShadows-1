using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using OOS.Shared;

namespace OOS.Game
{
    internal static class IntegrityManager
    {
        public sealed class Result
        {
            public string ReportPath { get; init; } = "";
            public int IssueCount { get; init; }          // -1 = no manifest, -2 = crash
            public bool ManifestFound { get; init; }
        }

        /// <summary>
        /// Validate sandbox against manifest; auto-repair where possible; write a human-readable report.
        /// Returns the report path and issue count.
        /// </summary>
        public static Result RunStartupCheck(string manifestPath, string sandboxRoot)
        {
            var report = new List<string>
            {
                "Office of Shadows – Integrity Check",
                $"Manifest: {manifestPath}",
                $"Sandbox : {sandboxRoot}",
                $"Date    : {DateTime.Now}",
                ""
            };

            try
            {
                if (!File.Exists(manifestPath))
                {
                    report.Add("ERROR: Manifest not found. Skipping validation.");
                    var missPath = WriteReport(sandboxRoot, report);
                    Debug.WriteLine($"Integrity: no manifest; report at {missPath}");
                    return new Result { ReportPath = missPath, IssueCount = -1, ManifestFound = false };
                }

                var manifest = SandboxManifest.Load(manifestPath);
                var issues = new List<Discrepancy>(manifest.Validate(sandboxRoot));

                if (issues.Count == 0)
                {
                    report.Add("All items match the manifest.");
                    var okPath = WriteReport(sandboxRoot, report);
                    Debug.WriteLine($"Integrity: ok; report at {okPath}");
                    return new Result { ReportPath = okPath, IssueCount = 0, ManifestFound = true };
                }

                report.Add($"Found {issues.Count} issue(s). Attempting auto-repair where possible…");
                report.Add("");

                foreach (var d in issues)
                {
                    report.Add($"- {d.Kind}: {d.Item.Path} ({d.Details})");

                    try
                    {
                        switch (d.Item.Kind)
                        {
                            case ItemKind.Directory:
                                EnsureDirectory(d.FullPath, report);
                                break;

                            case ItemKind.Shortcut:
                                RepairShortcut(d, sandboxRoot, report);
                                break;

                            case ItemKind.File:
                                RepairFile(d, report);
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        report.Add($"  Repair failed: {ex.Message}");
                    }
                }

                var written = WriteReport(sandboxRoot, report);
                Debug.WriteLine($"Integrity: repaired {issues.Count}; report at {written}");
                return new Result { ReportPath = written, IssueCount = issues.Count, ManifestFound = true };
            }
            catch (Exception ex)
            {
                report.Add("");
                report.Add($"FATAL: {ex.Message}");
                var written = WriteReport(sandboxRoot, report);
                Debug.WriteLine($"Integrity: crash; report at {written}");
                return new Result { ReportPath = written, IssueCount = -2, ManifestFound = false };
            }
        }

        // ---------- Repairs ----------

        private static void EnsureDirectory(string path, List<string> report)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                report.Add("  Repaired: Created directory.");
            }
        }

        private static void RepairShortcut(Discrepancy d, string sandboxRoot, List<string> report)
        {
            // Try a specific shortcut recreation; if not available, recreate all known shortcuts.
            var appName = AppNameFromShortcut(d.Item.Path);
            var ok = false;

            try
            {
                ok = ShortcutHelper.CreateShortcutForApp(
                        sandboxRoot,
                        d.Item.Path,                // e.g., "Terminal.lnk"
                        appName,                    // e.g., "OOS.Terminal"
                        "Office of Shadows tool");
            }
            catch
            {
                // ignore and try bulk creation
            }

            if (!ok)
            {
                try
                {
                    ShortcutHelper.CreateShortcutsIfMissing(sandboxRoot);
                    // confirm creation
                    ok = File.Exists(Path.Combine(sandboxRoot, d.Item.Path));
                }
                catch { /* ignore */ }
            }

            report.Add(ok
                ? "  Repaired: Recreated shortcut."
                : "  Could not repair: Target EXE not found.");
        }

        private static void RepairFile(Discrepancy d, List<string> report)
        {
            // If the manifest provides a source asset, copy it safely.
            if (!string.IsNullOrWhiteSpace(d.Item.Source))
            {
                var src = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, d.Item.Source);
                if (File.Exists(src))
                {
                    SafetyManager.SafeCopy(src, d.FullPath);
                    report.Add($"  Repaired: Copied from {d.Item.Source}");
                    return;
                }

                report.Add($"  Could not repair: Source not found: {src}");
                return;
            }

            // Special-case README (regenerate text if missing/corrupt and no source provided).
            if (string.Equals(Path.GetFileName(d.FullPath), "README.txt", StringComparison.OrdinalIgnoreCase))
            {
                SafetyManager.SafeWriteText(d.FullPath, DefaultReadme());
                report.Add("  Repaired: Regenerated README content.");
                return;
            }

            report.Add("  No repair source specified.");
        }

        // ---------- Helpers ----------

        /// <summary>
        /// Primary: write to EXE\FileValidation; fallback: sandbox\_integrity_report.txt.
        /// Returns actual path written.
        /// </summary>
        private static string WriteReport(string sandboxRoot, List<string> lines)
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var outDir = Path.Combine(baseDir, "FileValidation");
            var file = Path.Combine(outDir, $"integrity_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

            try
            {
                Directory.CreateDirectory(outDir);
                File.WriteAllLines(file, lines);
                return file;
            }
            catch
            {
                try
                {
                    var fallback = Path.Combine(sandboxRoot, "_integrity_report.txt");
                    File.WriteAllLines(fallback, lines);
                    return fallback;
                }
                catch
                {
                    return "";
                }
            }
        }

        private static string AppNameFromShortcut(string linkPath)
        {
            var name = Path.GetFileNameWithoutExtension(linkPath).ToLowerInvariant();
            return name switch
            {
                "terminal" => "OOS.Terminal",
                "vpn" => "OOS.VPN",
                "email" => "OOS.Email",
                _ => "OOS.Terminal"
            };
        }

        private static string DefaultReadme() =>
@"OFFICE OF SHADOWS – Liam’s Info

This folder is where your investigation tools, notes, and clues will appear.

If items are missing, the game will attempt to repair them automatically on startup.
You can reopen the game to rebuild critical files and shortcuts.

– RETIS Software";
    }
}
