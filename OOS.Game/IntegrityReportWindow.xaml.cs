using System.Windows;

namespace OOS.Game
{
    public partial class IntegrityReportWindow : Window
    {
        public IntegrityReportWindow(string reportText)
        {
            InitializeComponent();
            OutputBox.Text = reportText ?? "No details available.";
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
