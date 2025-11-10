using System;
using System.IO;
using System.Threading;
using System.Windows;
using OOS.Shared;

namespace OOS.Game
{
    public partial class App : Application
    {
        private static Mutex? _singleInstanceMutex;
        private GameRuntime? _runtime;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Keep the app alive even if there are no windows open.
            this.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Single-instance guard
            var createdNew = false;
            _singleInstanceMutex = new Mutex(initiallyOwned: true,
                name: "Global\\OfficeOfShadows_SingleInstance",
                createdNew: out createdNew);

            if (!createdNew)
            {
                MessageBox.Show("Office of Shadows is already running.", "Office of Shadows",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }

            // Crash logging hooks
            AppDomain.CurrentDomain.UnhandledException += (s, ev) =>
            {
                try { SharedLogger.Error("Fatal (AppDomain) exception:\n" + (ev.ExceptionObject as Exception)); }
                catch { }
            };
            this.DispatcherUnhandledException += (s, ev) =>
            {
                try { SharedLogger.Error("Fatal (Dispatcher) exception:\n" + ev.Exception); }
                catch { }
                ev.Handled = false;
            };

            // Load settings
            var settings = SettingsStore.Load();

            // Ensure Desktop sandbox exists
            var sandboxPath = SandboxHelper.EnsureSandboxFolder();

            // ----- Integrity check + small report window -----
            try
            {
                var exeDir = AppContext.BaseDirectory;
                var manifestPath = Path.Combine(exeDir, "Assets", "manifest.json");

                string reportText = IntegrityManager.RunStartupCheck(manifestPath, sandboxPath);

                var reportWindow = new IntegrityReportWindow(reportText);
                reportWindow.Show();
            }
            catch (Exception ex)
            {
                SharedLogger.Error("Integrity check failed:\n" + ex);
            }
            // -------------------------------------------------


            // Start background runtime (tray icon etc.)
            _runtime = new GameRuntime();
            _runtime.Start();

            // Determine first-run vs resume
            var progress = Progress.Load() ?? new Progress();
            var firstRun = string.IsNullOrWhiteSpace(progress.Checkpoint);

            try
            {
                if (firstRun)
                {
                    var intro = new IntroWindow();
                    intro.Show();
                    // Explorer will open later after the video ends (VideoWindow handles it).
                }
                else
                {
                    var resume = new ResumeWindow(progress);
                    resume.Show();
                    // Explorer opens after Continue (handled in ResumeWindow).
                }
            }
            catch (Exception ex)
            {
                SharedLogger.Error("Failed to open initial window:\n" + ex);
                MessageBox.Show(
                    "A critical error occurred while opening the game. Check the log for details.",
                    "Office of Shadows",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                _runtime?.Dispose();
                SharedLogger.Info("Application exiting.");
            }
            catch { }
            finally
            {
                _singleInstanceMutex?.ReleaseMutex();
                _singleInstanceMutex?.Dispose();
                _singleInstanceMutex = null;
            }
            base.OnExit(e);
        }
    }
}
