using System;
using System.Globalization;
using System.Windows;
using OOS.Shared;

namespace OOS.Game
{
    public partial class ResumeWindow : Window
    {
        private readonly Progress _progress;
        private readonly bool _hasSave;

        public ResumeWindow(Progress progress)
        {
            InitializeComponent();

            _progress = progress ?? new Progress();
            _hasSave = !string.IsNullOrWhiteSpace(_progress.Checkpoint);

            // --- Minimal UI updates to match your XAML exactly ---
            // Show timestamp when we have a save; otherwise show a simple "no save" line.
            if (_hasSave)
            {
                string stamp = "unknown";
                try
                {
                    var updatedUtc = _progress.UpdatedUtc;
                    if (updatedUtc != default)
                    {
                        var local = updatedUtc.ToLocalTime();
                        stamp = local.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                    }
                }
                catch { /* keep 'unknown' */ }

                // This TextBlock exists in your XAML as x:Name="LastSaveText"
                LastSaveText.Text = $"Last save: {stamp}   |   Checkpoint: {_progress.Checkpoint}";

                // Continue button visible/enabled if a save exists
                ContinueBtn.Visibility = Visibility.Visible;
                ContinueBtn.IsEnabled = true;

                // Keep your original button text
                // NewGameBtn.Content = "NEW GAME"; // already set in XAML
            }
            else
            {
                LastSaveText.Text = "No previous save found.";
                ContinueBtn.Visibility = Visibility.Collapsed; // hide to differentiate states
                // NewGameBtn.Content = "NEW GAME"; // leave as-is for minimal change
            }
        }

        // CONTINUE
        private void ContinueBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Open sandbox AFTER continue (for immersion)
                try
                {
                    var settings = SettingsStore.Load();
                    if (settings.OpenSandboxOnStart)
                        SandboxHelper.OpenSandboxFolder();
                }
                catch (Exception exOpen)
                {
                    SharedLogger.Warn("Opening sandbox folder failed from ResumeWindow (Continue):\n" + exOpen);
                }

                // Close only this window; app stays alive (OnExplicitShutdown + GameRuntime)
                this.Close();
            }
            catch (Exception ex)
            {
                SharedLogger.Error("ResumeWindow ContinueBtn_Click failed:\n" + ex);
                MessageBox.Show("Something went wrong while continuing the game.",
                    "Office of Shadows", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // NEW GAME
        private void NewGameBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var p = new Progress { Checkpoint = "intro" };
                p.Save(); // instance method in your codebase

                var video = new VideoWindow();
                video.Show();

                this.Close();
            }
            catch (Exception ex)
            {
                SharedLogger.Error("ResumeWindow NewGameBtn_Click failed:\n" + ex);
                MessageBox.Show("Failed to start a new game.",
                    "Office of Shadows", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // If you wire an Exit button in this XAML later:
        private void ExitBtn_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}
