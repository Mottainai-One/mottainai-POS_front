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
            CustomerFound = digits.Length == 11;
            IsMottainaiMember = digits == "00000000000";
            CustomerName = IsMottainaiMember ? "Demo Customer" : "Cliente comum";
            CustomerCpf = digits.Length == 11 ? $"{digits[..3]}.{digits[3..6]}.{digits[6..9]}-{digits[9..]}" : string.Empty;
            ResultText.Text = IsMottainaiMember
                ? "Demo Customer · membro Mottainai · promoções ativas"
                : CustomerFound
                    ? "Cliente comum · sem promoções Mottainai"
                    : "CPF inválido. Digite os 11 números do documento.";
            ResultText.Foreground = CustomerFound
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(16, 42, 67))
                : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(186, 78, 33));
        }
    }
}
