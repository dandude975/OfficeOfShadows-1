using OOS.Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace OOS.Game
{
    public partial class App : Application
    {
        private Window? _hostWindow;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Show any exceptions instead of “silent nothing”
            this.DispatcherUnhandledException += (s, ev) =>
            {
                MessageBox.Show(ev.Exception.ToString(), "Unhandled UI Error", MessageBoxButton.OK, MessageBoxImage.Error);
                ev.Handled = true;
            };
            AppDomain.CurrentDomain.UnhandledException += (s, ev) =>
            {
                var ex = ev.ExceptionObject as Exception;
                MessageBox.Show(ex?.ToString() ?? "Non-Exception crash", "Unhandled BG Error", MessageBoxButton.OK, MessageBoxImage.Error);
            };
            TaskScheduler.UnobservedTaskException += (s, ev) =>
            {
                MessageBox.Show(ev.Exception.ToString(), "Unobserved Task Error", MessageBoxButton.OK, MessageBoxImage.Error);
                ev.SetObserved();
            };

            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Invisible host to anchor lifetime
            _hostWindow = new Window
            {
                Width = 0,
                Height = 0,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Opacity = 0
            };
            _hostWindow.Show();
            MainWindow = _hostWindow;

            // --- Integrity (unchanged) ---
            var installRoot = AppDomain.CurrentDomain.BaseDirectory;
            var sandboxPath = ResolveSandboxPath();
            var mgr = new IntegrityManager();
            var log = new List<string>();
            try
            {
                mgr.RunStartupCheck(sandboxPath, installRoot, s =>
                {
                    log.Add(s);
                    Debug.WriteLine(s);
                }).GetAwaiter().GetResult();
            }
            catch (Exception ex) { log.Add($"[Integrity] Fatal error: {ex}"); }

            var tail = string.Join(Environment.NewLine, log.TakeLast(12));
            MessageBox.Show(string.IsNullOrWhiteSpace(tail) ? "Integrity ran (no log lines)" : tail,
                            "System Integrity Verification",
                            MessageBoxButton.OK, MessageBoxImage.Information);

            // --- TEMP: Visible ping window so you *always* see *something* ---
            var ping = new Window
            {
                Title = "OOS Ping",
                Width = 320,
                Height = 100,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Topmost = true,
                Content = new System.Windows.Controls.TextBlock
                {
                    Text = "UI is alive.\nLaunching Resume...",
                    Margin = new Thickness(16),
                    Foreground = System.Windows.Media.Brushes.White
                },
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(32, 32, 32))
            };
            ping.Show();

            // Close the ping after a moment so it doesn’t linger
            Dispatcher.InvokeAsync(async () =>
            {
                await Task.Delay(3000);
                if (ping.IsVisible) ping.Close();
            });

            // --- Show Resume window (NO OWNER; force CenterScreen + Topmost for first show) ---
            try
            {
                var progress = new Progress(); // use your project’s Progress type if namespaced differently
                var resume = new ResumeWindow(progress)
                {
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    Topmost = true // only for first paint—user can alt-tab it afterwards
                };
                resume.Loaded += (_, __) => resume.Topmost = false; // drop Topmost after showing once
                resume.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open Resume window:\n{ex}", "OOS", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string ResolveSandboxPath()
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var root = Path.Combine(desktop, "Office Work Stuff");
            Directory.CreateDirectory(root);
            return root;
        }
    }
}
