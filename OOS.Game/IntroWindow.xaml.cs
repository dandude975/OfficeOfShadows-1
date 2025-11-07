using System.Windows;

namespace OOS.Game
{
    public partial class IntroWindow : Window
    {
        public bool UserConsented { get; private set; }

        // Expose where this window was located when the user clicked Start
        public double SavedLeft { get; private set; }
        public double SavedTop { get; private set; }

        public IntroWindow()
        {
            InitializeComponent();
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (AcknowledgeCheck.IsChecked == true)
            {
                // capture current position BEFORE closing
                SavedLeft = this.Left;
                SavedTop = this.Top;

                UserConsented = true;
                Close();
            }
            else
            {
                MessageBox.Show("Please confirm you understand before continuing.", "Office of Shadows");
            }
        }
    }
}
