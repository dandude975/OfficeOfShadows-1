using System;
using System.IO;

namespace OOS.Shared
{
    public static class SharedPaths
    {
        public static string SandboxFolderName => "Office Work Stuff";

        public static string DesktopSandbox =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), SandboxFolderName);

        public static string LocalAppDataRoot =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OfficeOfShadows");

        public static string Logs => Ensure(Path.Combine(LocalAppDataRoot, "logs"));
        public static string Queue => Ensure(Path.Combine(LocalAppDataRoot, "queue"));          // JSONL inbox
        public static string Outbox => Ensure(Path.Combine(LocalAppDataRoot, "outbox"));       // optional
        public static string Saves => Ensure(Path.Combine(LocalAppDataRoot, "saves"));
        public static string Config => Ensure(Path.Combine(LocalAppDataRoot, "config"));
        public static string Scripts => Ensure(Path.Combine(LocalAppDataRoot, "scripts"));

        public static string ProgressFile => Path.Combine(Saves, "progress.json");
        public static string TimelineFile => Path.Combine(Scripts, "timeline.json");

        private static string Ensure(string dir) { Directory.CreateDirectory(dir); return dir; }
    }
}
