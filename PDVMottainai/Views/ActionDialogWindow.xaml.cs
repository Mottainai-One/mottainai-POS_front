using System.Windows;
using System.Windows.Controls;

namespace PDVMottainai.Views
{
    public partial class ActionDialogWindow : Window
    {
        public Func<bool>? ConfirmAction { get; set; }
        public Action? CancelAction { get; set; }

        public ActionDialogWindow(string title, string subtitle, string primaryText, UserControl content,
            bool fullScreen = false, double height = 520, string cancelText = "Voltar")
        {
            InitializeComponent();
            Height = height;
            if (fullScreen)
            {
                WindowState = WindowState.Maximized;
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
                MaximizeButton.Content = "❐";
                MaximizeButton.ToolTip = "Restaurar";
            }
            DialogTitle.Text = title;
            DialogSubtitle.Text = subtitle;
            PrimaryButton.Content = primaryText;
            CancelButton.Content = cancelText;
            DialogContent.Content = content;
        }

        private void PrimaryButton_Click(object sender, RoutedEventArgs e)
        {
            if (ConfirmAction?.Invoke() == false)
                return;

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            CancelAction?.Invoke();
            DialogResult = false;
            Close();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void MaximizeRestore_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            MaximizeButton.Content = WindowState == WindowState.Maximized ? "❐" : "□";
            MaximizeButton.ToolTip = WindowState == WindowState.Maximized ? "Restaurar" : "Maximizar";
        }

        private void CloseWindow_Click(object sender, RoutedEventArgs e) => Close();
    }
}
