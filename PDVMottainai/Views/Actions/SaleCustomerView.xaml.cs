using System.Windows;
using System.Windows.Controls;

namespace PDVMottainai.Views.Actions
{
    public partial class SaleCustomerView : UserControl
    {
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
            HasInvalidCpf = digits.Length > 0;
            CustomerCpf = string.Empty;
            IsMottainaiMember = false;
            CustomerName = "Consumidor não identificado";

            CustomerStatusText.Text = CustomerName;
            MembershipText.Text = "Customer lookup is unavailable in this POS build. Continue without customer identification.";
            ValidationText.Text = digits.Length == 11 ? "Customer lookup is unavailable in this POS build." : string.Empty;
            CustomerStatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(102, 119, 132));
        }
    }
}
