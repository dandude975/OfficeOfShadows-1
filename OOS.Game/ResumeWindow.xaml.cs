using System;
using System.Windows;
using OOS.Shared;

namespace OOS.Game
{
    public partial class ResumeWindow : Window
    {
        public enum Choice { None, Continue, NewGame }
        public Choice Result { get; private set; } = Choice.None;

        public ResumeWindow(Progress progress)
        {
            InitializeComponent();

            // Show last checkpoint + local time of last update
            var local = progress.UpdatedUtc.ToLocalTime();
            LastSaveText.Text = $"Last checkpoint: {progress.Checkpoint}  •  {local:dd MMM yyyy, HH:mm}";
        }

        private void ContinueBtn_Click(object sender, RoutedEventArgs e)
        {
            Result = Choice.Continue;
            Close();
        }

        private void NewGameBtn_Click(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show(
                "Start a new game? Your current progress will be reset.",
                "Office of Shadows",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm == MessageBoxResult.Yes)
            {
                Result = Choice.NewGame;
                Close();
            }
        }
    }
}
