using System;
using System.IO;

namespace OOS.Game
{
    public static class SandboxHelper
    {
        public static string SandboxFolderName => "Office Work Stuff";

        public static string EnsureSandboxFolder()
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var path = Path.Combine(desktop, SandboxFolderName);
            Directory.CreateDirectory(path);

            var readme = Path.Combine(path, "README.txt");
            if (!File.Exists(readme))
                File.WriteAllText(readme, "\n\nAs you saw from the video, shit's getting real, " +
                    "you gotta help me out!" +
                    "\nI think they're watching me.");

            return path;
        }
    }
}
