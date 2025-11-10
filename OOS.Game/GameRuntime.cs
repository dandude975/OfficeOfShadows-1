using System;
using SD = System.Drawing;
using SWF = System.Windows.Forms;
using OOS.Shared;

namespace OOS.Game
{
    /// <summary>
    /// Minimal long-lived runtime so the app can keep running headlessly (no visible window).
    /// Provides a tray icon with "Open Sandbox Folder" and "Quit" items.
    /// </summary>
    public sealed class GameRuntime : IDisposable
    {
        private SWF.NotifyIcon? _tray;
        private SWF.ContextMenuStrip? _menu;

        public GameRuntime()
        {
            // Intentionally empty – we create UI objects in Start()
        }

        public void Start()
        {
            try
            {
                _tray = new SWF.NotifyIcon
                {
                    Visible = true,
                    Text = "Office of Shadows",
                    Icon = SD.SystemIcons.Application
                };

                _menu = new SWF.ContextMenuStrip();

                var openSandbox = new SWF.ToolStripMenuItem("Open Sandbox Folder");
                openSandbox.Click += (_, __) =>
                {
                    try { SandboxHelper.OpenSandboxFolder(); }
                    catch (Exception ex) { SharedLogger.Warn("Tray: open sandbox failed:\n" + ex); }
                };

                var quit = new SWF.ToolStripMenuItem("Quit");
                quit.Click += (_, __) =>
                {
                    try { System.Windows.Application.Current.Shutdown(); } catch { }
                };

                _menu.Items.Add(openSandbox);
                _menu.Items.Add(quit);

                _tray.ContextMenuStrip = _menu;
            }
            catch (Exception ex)
            {
                SharedLogger.Warn("GameRuntime tray initialization failed:\n" + ex);
            }
        }

        public void Dispose()
        {
            try
            {
                if (_tray != null)
                {
                    _tray.Visible = false;
                    _tray.ContextMenuStrip = null;
                    _tray.Dispose();
                    _tray = null;
                }
            }
            catch { /* ignore */ }

            try
            {
                _menu?.Dispose();
                _menu = null;
            }
            catch { /* ignore */ }
        }
    }
}
