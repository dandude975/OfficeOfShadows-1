using System;
using System.Collections.Generic;
using System.IO;
using System.Timers;
using System.Windows;

namespace OOS.Game
{
    public sealed class BackgroundManager
    {
        public static BackgroundManager Instance { get; } = new BackgroundManager();

        private System.Timers.Timer? _timer;
        private readonly List<Action> _events = new();
        private int _idx;
        private readonly string _sandbox = SandboxHelper.EnsureSandboxFolder();

        private BackgroundManager() { }

        public void Start()
        {
            // Schedule some example events (replace with your own / JSON-driven)
            _events.Add(() => DropFile("we_are_watching.txt", "we are watching you."));
            _events.Add(() => ShowMessage("Incoming message", "Check the folder..."));
            _events.Add(() => ShowTerminalPopup());

            _timer = new Timer(30_000); // every 30s
            _timer.Elapsed += (s, e) => Application.Current.Dispatcher.Invoke(RunNextEvent);
            _timer.Start();
        }

        public void Stop()
        {
            _timer?.Stop();
            _timer?.Dispose();
            _timer = null;
        }

        private void RunNextEvent()
        {
            if (_events.Count == 0) return;
            var action = _events[_idx % _events.Count];
            _idx++;
            action();
        }

        public void ResetEverything()
        {
            Stop();
            try
            {
                if (Directory.Exists(_sandbox))
                {
                    Directory.Delete(_sandbox, true);
                    Directory.CreateDirectory(_sandbox);
                }
            }
            catch { /* ignore */ }
            Start();
        }

        private void DropFile(string name, string content)
        {
            var path = Path.Combine(_sandbox, name);
            File.WriteAllText(path, content);
        }

        private void ShowMessage(string title, string body)
        {
            // For dev simplicity; later swap to Windows toasts (Toolkit) if you want
            MessageBox.Show(body, title);
        }

        private void ShowTerminalPopup()
        {
            try
            {
                var win = new OOS.Terminal.CommandWindow();
                win.Topmost = true;
                win.Show();
            }
            catch
            {
                // If Terminal project isn't available, skip
            }
        }
    }
}
