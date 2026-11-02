using System.Windows;
using System.Windows.Controls;

namespace PDVMottainai.Views.Actions
{
    public partial class FindCustomerView : UserControl
    {
        public bool CustomerFound { get; private set; }
        public bool IsMottainaiMember { get; private set; }
        public string CustomerName { get; private set; } = "Cliente comum";
        public string CustomerCpf { get; private set; } = string.Empty;
        public void ShowFeedback(string message) => ResultText.Text = message;

        public FindCustomerView() => InitializeComponent();

        private void SearchCustomer_Click(object sender, RoutedEventArgs e)
        {
            var digits = new string(CpfTextBox.Text.Where(char.IsDigit).ToArray());
            CustomerFound = false;
            IsMottainaiMember = false;
            CustomerName = "Consumidor não identificado";
            CustomerCpf = string.Empty;
            ResultText.Text = digits.Length == 11
                ? "Customer lookup is unavailable in this POS build. The CPF was not sent."
                : "Customer lookup is unavailable in this POS build.";
            ResultText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(186, 78, 33));
        }
    }
}
