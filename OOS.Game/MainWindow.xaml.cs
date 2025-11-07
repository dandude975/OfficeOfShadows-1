using System.Windows;

namespace OOS.Game
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void OpenTerminal_Click(object sender, RoutedEventArgs e)
        {
            var win = new OOS.Terminal.CommandWindow();
            win.Show();
        }
    }
}
