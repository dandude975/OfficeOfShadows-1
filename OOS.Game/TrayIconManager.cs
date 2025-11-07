using System.Windows.Forms;        // NotifyIcon, ContextMenuStrip, ToolStripMenuItem
using WF = System.Windows.Forms;   // alias for forms
using WPF = System.Windows;        // alias for WPF


namespace OOS.Game
{
    public static class TrayIconManager
    {
        private static WF.NotifyIcon? _tray;

        public static void CreateTrayIcon()
        {
            if (_tray != null) return;

            _tray = new NotifyIcon
            {
                Icon = System.Drawing.SystemIcons.Application,
                Visible = true,
                Text = "Office of Shadows (running)"
            };

            var menu = new ContextMenuStrip();

            var reset = new ToolStripMenuItem("Reset / Purge");
            reset.Click += (s, e) => BackgroundManager.Instance.ResetEverything();
            menu.Items.Add(reset);

            var quit = new ToolStripMenuItem("Quit");
            quit.Click += (s, e) => WPF.Application.Current.Dispatcher
                .Invoke(() => WPF.Application.Current.Shutdown()); menu.Items.Add(quit);

            _tray.ContextMenuStrip = menu;
            _tray.DoubleClick += (s, e) =>
                WPF.MessageBox.Show("Game is running in the background.", "Office of Shadows");
        }

        public static void DisposeTrayIcon()
        {
            _tray?.Dispose();
            _tray = null;
        }
    }
}
