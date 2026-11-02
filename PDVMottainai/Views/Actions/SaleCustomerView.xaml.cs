using System.Windows;
using System.Windows.Controls;
using System.Net;
using PDVMottainai.Services;

namespace PDVMottainai.Views.Actions
{
    public partial class SaleCustomerView : UserControl
    {
        private readonly PosApiClient _api;
        private string _verifiedCpf = string.Empty;

        public bool IsMottainaiMember { get; private set; }
        public bool HasInvalidCpf { get; private set; }
        public bool WantsCpfOnInvoice => InvoiceYes.IsChecked == true;
        public string CustomerCpf { get; private set; } = string.Empty;
        public string CustomerName { get; private set; } = "Consumidor não identificado";
        public int? CustomerId { get; private set; }

        public SaleCustomerView(PosApiClient api)
        {
            _api = api;
            InitializeComponent();
        }

        private async void VerifyCpf_Click(object sender, RoutedEventArgs e)
        {
            var digits = NormalizeCpf(CpfTextBox.Text);
            if (!IsValidCpf(digits))
            {
                ResetLookup();
                HasInvalidCpf = digits.Length > 0;
                ValidationText.Text = "Informe um CPF válido com 11 dígitos.";
                return;
            }

            CustomerCpf = digits;
            ValidationText.Text = "Verificando cadastro Mottainai...";
            CpfTextBox.IsEnabled = false;
            VerifyCpfButton.IsEnabled = false;
            try
            {
                var customer = await _api.SearchCustomerAsync(digits);
                CustomerId = customer.CustomerId;
                CustomerName = customer.DisplayName;
                IsMottainaiMember = true;
                _verifiedCpf = digits;
                CustomerStatusText.Text = CustomerName;
                MembershipText.Text = "Cadastro Mottainai confirmado. Promoções elegíveis serão aplicadas à venda.";
                ValidationText.Text = string.Empty;
                CustomerStatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(27, 128, 83));
            }
            catch (PosApiException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
            {
                ResetLookup(keepCpf: true);
                _verifiedCpf = digits;
                MembershipText.Text = "Este CPF não está associado a um cadastro Mottainai ativo. Você ainda pode usá-lo na nota fiscal.";
                ValidationText.Text = string.Empty;
            }
            catch (PosApiException exception)
            {
                ResetLookup(keepCpf: true);
                MembershipText.Text = "Não foi possível verificar o cadastro. A venda pode continuar sem aplicar promoções.";
                ValidationText.Text = $"POS API: {exception.Message}";
            }
            catch (HttpRequestException)
            {
                ResetLookup(keepCpf: true);
                MembershipText.Text = "Não foi possível verificar o cadastro. A venda pode continuar sem aplicar promoções.";
                ValidationText.Text = "Não foi possível conectar à API do PDV.";
            }
            catch (TaskCanceledException)
            {
                ResetLookup(keepCpf: true);
                MembershipText.Text = "Não foi possível verificar o cadastro. A venda pode continuar sem aplicar promoções.";
                ValidationText.Text = "A consulta expirou. Verifique a API do PDV e tente novamente.";
            }
            finally
            {
                CpfTextBox.IsEnabled = true;
                VerifyCpfButton.IsEnabled = true;
            }
        }

        public void ShowFeedback(string message) => ValidationText.Text = message;

        public void DiscardIncompleteCpf()
        {
            CpfTextBox.Clear();
            ResolveCustomer();
        }

        public void ResolveCustomer()
        {
            var digits = NormalizeCpf(CpfTextBox.Text);
            HasInvalidCpf = digits.Length > 0 && !IsValidCpf(digits);
            CustomerCpf = IsValidCpf(digits) ? digits : string.Empty;

            if (digits != _verifiedCpf)
                ResetLookup(keepCpf: digits.Length > 0);
            if (digits.Length == 0)
            {
                _verifiedCpf = string.Empty;
                ResetLookup();
                MembershipText.Text = "As promoções Mottainai são aplicadas a clientes cadastrados.";
                ValidationText.Text = string.Empty;
            }
            else if (HasInvalidCpf)
            {
                ValidationText.Text = "Informe um CPF válido com 11 dígitos.";
            }
        }

        private void ResetLookup(bool keepCpf = false)
        {
            CustomerId = null;
            IsMottainaiMember = false;
            CustomerName = "Consumidor não identificado";
            if (!keepCpf)
                CustomerCpf = string.Empty;
            CustomerStatusText.Text = CustomerName;
            MembershipText.Text = keepCpf
                ? "Este CPF ainda não foi validado como conta Mottainai."
                : "As promoções Mottainai são aplicadas a clientes cadastrados.";
            CustomerStatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(102, 119, 132));
        }

        private static string NormalizeCpf(string? value) =>
            new((value ?? string.Empty).Where(char.IsDigit).ToArray());

        private static bool IsValidCpf(string digits)
        {
            if (digits.Length != 11 || digits.All(digit => digit == digits[0]))
                return false;

            var firstSum = 0;
            for (var index = 0; index < 9; index++)
                firstSum += (digits[index] - '0') * (10 - index);
            var firstDigit = firstSum % 11 < 2 ? 0 : 11 - firstSum % 11;
            if (digits[9] - '0' != firstDigit)
                return false;

            var secondSum = 0;
            for (var index = 0; index < 10; index++)
                secondSum += (digits[index] - '0') * (11 - index);
            var secondDigit = secondSum % 11 < 2 ? 0 : 11 - secondSum % 11;
            return digits[10] - '0' == secondDigit;
        }
    }
}
