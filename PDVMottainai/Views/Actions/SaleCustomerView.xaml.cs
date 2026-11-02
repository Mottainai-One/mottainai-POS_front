using System.Windows;
using System.Windows.Controls;

namespace PDVMottainai.Views.Actions
{
    public partial class SaleCustomerView : UserControl
    {
        private bool _verified;
        public bool IsMottainaiMember { get; private set; }
        public bool HasInvalidCpf { get; private set; }
        public bool WantsCpfOnInvoice => InvoiceYes.IsChecked == true;
        public string CustomerCpf { get; private set; } = string.Empty;
        public string CustomerName { get; private set; } = "Consumidor não identificado";

        public SaleCustomerView() => InitializeComponent();

        private void VerifyCpf_Click(object sender, RoutedEventArgs e) => ResolveCustomer();
        public void ShowFeedback(string message) => ValidationText.Text = message;

        public void DiscardIncompleteCpf()
        {
            CpfTextBox.Clear();
            ResolveCustomer();
        }

        public void ResolveCustomer()
        {
            var digits = new string(CpfTextBox.Text.Where(char.IsDigit).ToArray());
            HasInvalidCpf = digits.Length > 0 && digits.Length != 11;
            ValidationText.Text = string.Empty;
            CustomerCpf = digits;
            IsMottainaiMember = digits == "00000000000";
            CustomerName = IsMottainaiMember
                ? "Demo Customer"
                : digits.Length == 11 ? "Cliente comum" : "Consumidor não identificado";
            _verified = digits.Length == 11;

            CustomerStatusText.Text = CustomerName;
            MembershipText.Text = IsMottainaiMember
                ? "Cliente Mottainai identificado. As promoções disponíveis serão aplicadas."
                : _verified
                    ? "Cliente não cadastrado como Mottainai. As promoções não serão aplicadas."
                    : "CPF não informado. A venda seguirá como consumidor não identificado.";
            CustomerStatusText.Foreground = IsMottainaiMember
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(16, 42, 67))
                : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(102, 119, 132));
        }
    }
}
