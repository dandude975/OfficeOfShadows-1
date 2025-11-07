using System;
using System.IO;

namespace OOS.Game
{
    internal static class SafetyManager
    {
        /// <summary>
        /// Copy src -> dest, backing up the existing dest into FileValidation\Backups\<timestamp>\.
        /// </summary>
        public static void SafeCopy(string src, string dest)
        {
            var backupDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                                         "FileValidation", "Backups",
                                         DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            Directory.CreateDirectory(backupDir);

            if (File.Exists(dest))
            {
                var destBackup = Path.Combine(backupDir, Path.GetFileName(dest));
                File.Copy(dest, destBackup, overwrite: true);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(src, dest, overwrite: true);
        }

        /// <summary>
        /// Atomically write text to a file with a backup of existing content.
        /// </summary>
        public static void SafeWriteText(string dest, string contents)
        {
            var temp = dest + ".tmp";
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);

            // backup current
            if (File.Exists(dest))
            {
                var backupDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                                             "FileValidation", "Backups",
                                             DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                Directory.CreateDirectory(backupDir);
                File.Copy(dest, Path.Combine(backupDir, Path.GetFileName(dest)), overwrite: true);
            }

            File.WriteAllText(temp, contents);
            if (File.Exists(dest))
                File.Replace(temp, dest, null); // atomic replace
            else
                File.Move(temp, dest);
        }
    }
}
