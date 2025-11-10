using System;
using System.Diagnostics;
using System.IO;

namespace OOS.Shared
{
    /// <summary>
    /// Creates/maintains the Desktop sandbox folder and offers helpers for UX.
    /// </summary>
    public static class SandboxHelper
    {
        /// <summary>
        /// Returns the Desktop sandbox path (e.g., "Desktop\Office Work Stuff").
        /// If missing, creates it and seeds a README.
        /// </summary>
        public static string EnsureSandboxFolder()
        {
            // If your project already exposes this via SharedPaths, keep it.
            // Here we resolve a sane default if not present.
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var sandboxPath = Path.Combine(desktop, "Office Work Stuff");

            if (!Directory.Exists(sandboxPath))
                Directory.CreateDirectory(sandboxPath);

            SeedReadme(sandboxPath);
            return sandboxPath;
        }

        /// <summary>
        /// Opens the sandbox folder in File Explorer.
        /// </summary>
        public static void OpenSandboxFolder()
        {
            var path = EnsureSandboxFolder();
            if (!Directory.Exists(path)) return;

            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true // required to let the shell (Explorer) handle folders
            });
        }

        private static void SeedReadme(string sandboxPath)
        {
            var readme = Path.Combine(sandboxPath, "README.txt");
            if (File.Exists(readme)) return;

            var text =
@"Office of Shadows — Desktop Sandbox

This folder is a safe in-game workspace. Files here may be created,
modified, or removed by puzzles, scripts, and the in-game Terminal.

Tips
 • You can open this folder any time from the game.
 • Nothing here touches your real system outside this sandbox.

— Stay vigilant.";
            File.WriteAllText(readme, text);
        }
    }
}
