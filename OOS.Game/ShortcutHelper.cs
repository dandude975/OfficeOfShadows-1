using System;
using System.IO;
using System.Linq;

// If you use COM shortcuts, ensure you have a COM reference:
// Project > Add Reference > COM > "Windows Script Host Object Model"
// and: using IWshRuntimeLibrary;

namespace OOS.Game
{
    internal static class ShortcutHelper
    {
        public static void CreateShortcutsIfMissing(string sandboxRoot)
        {
            CreateShortcutForApp(sandboxRoot, "Terminal.lnk", "OOS.Terminal", "Office of Shadows - Terminal");
            CreateShortcutForApp(sandboxRoot, "VPN.lnk", "OOS.VPN", "Office of Shadows - VPN");
            CreateShortcutForApp(sandboxRoot, "Email.lnk", "OOS.Email", "Office of Shadows - Email");
        }

        public static bool CreateShortcutForApp(string folder, string linkName, string exeProjectName, string description)
        {
            var target = ResolveExe(exeProjectName);
            var linkPath = Path.Combine(folder, linkName);

            if (target == null || !File.Exists(target))
            {
                // Could log here if you have SharedLogger available in Game
                return false;
            }

            Directory.CreateDirectory(folder);
            CreateLnk(linkPath, target, Path.GetDirectoryName(target)!, description);
            return File.Exists(linkPath);
        }

        private static string? ResolveExe(string exeProjectName)
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var exeName = exeProjectName + ".exe";

            // 1) Same folder as the game (publish scenario)
            var sameDir = Path.Combine(baseDir, exeName);
            if (File.Exists(sameDir)) return sameDir;

            // 2) Dev: search sibling project bin folders relative to the Game bin folder
            // Walk up to solution root (heuristic: up to 5 parents)
            var dir = new DirectoryInfo(baseDir);
            for (int i = 0; i < 5 && dir?.Parent != null; i++) dir = dir.Parent; // up from ...\OOS.Game\bin\Debug\net8.0-windows

            if (dir != null && dir.Exists)
            {
                // Typical path: [solution]\OOS.Terminal\bin\Debug\[tfm]\OOS.Terminal.exe
                var projectDir = new DirectoryInfo(Path.Combine(dir.FullName, exeProjectName));
                if (projectDir.Exists)
                {
                    var binDir = new DirectoryInfo(Path.Combine(projectDir.FullName, "bin"));
                    if (binDir.Exists)
                    {
                        // Prefer current config (Debug/Release) if we can detect it
                        var candidates = binDir.EnumerateDirectories("*", SearchOption.AllDirectories)
                                               .SelectMany(d => d.EnumerateFiles(exeName, SearchOption.TopDirectoryOnly))
                                               .OrderByDescending(f => f.LastWriteTimeUtc)
                                               .ToList();
                        var hit = candidates.FirstOrDefault();
                        if (hit != null) return hit.FullName;
                    }
                }
            }

            return null;
        }

        private static void CreateLnk(string shortcutPath, string targetExe, string workingDir, string description)
        {
            try
            {
                var shell = new IWshRuntimeLibrary.WshShell();
                var shortcut = (IWshRuntimeLibrary.IWshShortcut)shell.CreateShortcut(shortcutPath);
                shortcut.TargetPath = targetExe;
                shortcut.WorkingDirectory = workingDir;
                shortcut.Description = description;

                // Use the real Command Prompt icon
                var systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                var cmdIcon = Path.Combine(systemRoot, "System32", "cmd.exe");
                shortcut.IconLocation = $"{cmdIcon},0";

                // (Optional) open Terminal in your sandbox folder by default
                // shortcut.WorkingDirectory = OOS.Shared.SharedPaths.DesktopSandbox;

                shortcut.Save();
            }
            catch
            {
                // .url fallback kept if you want, but it won't carry a custom icon.
                if (!shortcutPath.EndsWith(".url", StringComparison.OrdinalIgnoreCase))
                    shortcutPath = Path.ChangeExtension(shortcutPath, ".url");

                File.WriteAllText(shortcutPath,
                    "[InternetShortcut]" + Environment.NewLine +
                    $"URL=file:///{targetExe.Replace("\\", "/")}" + Environment.NewLine);
            }
        }

    }
}
