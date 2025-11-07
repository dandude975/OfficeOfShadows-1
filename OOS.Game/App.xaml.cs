using OOS.Shared;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace OOS.Game
{
    public partial class App : Application
    {
        private StoryController _story = new();

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            if (!Progress.Exists())
            {
                if (!await ShowIntroAndVideoAsync()) { Shutdown(); return; }
            }
            else
            {
                var resume = new ResumeWindow(_story.Progress);
                resume.ShowDialog();

                if (resume.Result == ResumeWindow.Choice.NewGame)
                {
                    _story.ResetProgress();
                    if (!await ShowIntroAndVideoAsync()) { Shutdown(); return; }
                }
                // Continue = skip intro/video
            }

            var sandboxPath = SandboxHelper.EnsureSandboxFolder();
            ShortcutHelper.CreateShortcutsIfMissing(sandboxPath);

            var manifestPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "manifest.json");
            var integrity = IntegrityManager.RunStartupCheck(manifestPath, sandboxPath);

            // Optional: quick toast to tell you where the report went (helpful while developing)
            System.Diagnostics.Debug.WriteLine($"Integrity: report={integrity.ReportPath}, issues={integrity.IssueCount}");

            if (!string.IsNullOrEmpty(integrity.ReportPath))
            {
                System.Windows.MessageBox.Show(
                    $"Integrity check complete.\nIssues: {integrity.IssueCount}\nReport: {integrity.ReportPath}",
                    "Office of Shadows");
            }


            _story.SetCheckpoint("tools_opened");

            BackgroundManager.Instance.Start();
            TrayIconManager.CreateTrayIcon();
        }

        private async Task<bool> ShowIntroAndVideoAsync()
        {
            var intro = new IntroWindow();
            intro.ShowDialog();
            if (!intro.UserConsented) return false;

            var left = intro.Left; var top = intro.Top;
            var clipPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "excoworker_clip.mp4");
            if (File.Exists(clipPath))
            {
                var video = new VideoWindow(clipPath, left, top);
                await video.PlayAsync();
            }

            _story.SetCheckpoint("video_played");
            return true;
        }
    }
}
