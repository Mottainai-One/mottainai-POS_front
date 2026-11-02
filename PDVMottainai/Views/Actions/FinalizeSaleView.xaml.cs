using PDVMottainai.Models;
using PDVMottainai.Services;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace PDVMottainai.Views.Actions
{
    public partial class FinalizeSaleView : UserControl
    {
        private readonly decimal _total;
        private readonly CultureInfo _culture = CultureInfo.GetCultureInfo("pt-BR");
        private readonly int _saleNumber;
        private string? _currentPixPayload;
        private string? _currentPixReference;
        public ObservableCollection<PaymentSplit> PaymentSplits { get; } = new();
        public decimal Remaining => Math.Max(0m, _total - PaymentSplits.Sum(payment => payment.Amount));
        public bool IsPaymentComplete => PaymentSplits.Count > 0 && Remaining <= 0.009m;

        public FinalizeSaleView(ObservableCollection<ScannedProductLine> items, decimal total,
            string customerName, string customerCpf, bool cpfOnInvoice, int saleNumber)
        {
            InitializeComponent();
            _total = total;
            _saleNumber = saleNumber;
            SaleItemsList.ItemsSource = items;
            PaymentSplitsList.ItemsSource = PaymentSplits;
            TotalTextBlock.Text = total.ToString("C", _culture);
            CustomerTextBlock.Text = cpfOnInvoice && !string.IsNullOrWhiteSpace(customerCpf)
                ? $"{customerName} · CPF na nota: {customerCpf}"
                : customerName;
            UpdatePaymentSummary();
            PaymentAmountTextBox.Text = total.ToString("0.00", _culture);
            UpdateMethodPanels();
        }

        public string PaymentSummary => string.Join(", ", PaymentSplits.Select(payment =>
            $"{payment.Method} {payment.Amount.ToString("C", _culture)}"));

        public IReadOnlyList<SalePaymentRecord> GetPaymentRecords() => PaymentSplits
            .Select(payment => new SalePaymentRecord
            {
                Method = payment.Method,
                Amount = payment.Amount,
                CashReceived = payment.CashReceived,
                Change = payment.Change,
                Reference = payment.Reference
            }).ToList();

        public void ShowFeedback(string message) => FeedbackTextBlock.Text = message;

        private void PaymentMethodComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsInitialized)
                UpdateMethodPanels();
        }

        private void UpdateMethodPanels()
        {
            var method = SelectedMethod;
            CashPanel.Visibility = method == "Dinheiro" ? Visibility.Visible : Visibility.Collapsed;
            PixPanel.Visibility = method == "Pix" ? Visibility.Visible : Visibility.Collapsed;
            OtherPaymentPanel.Visibility = method is "Pix" or "Dinheiro" ? Visibility.Collapsed : Visibility.Visible;
            UpdateChange();
            UpdatePixInformation();
        }

        private string SelectedMethod =>
            (PaymentMethodComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Pix";

        private void PaymentAmountTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsInitialized) return;
            UpdateChange();
            UpdatePixInformation();
        }

        private void CashReceivedTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (IsInitialized)
                UpdateChange();
        }

        private void UpdateChange()
        {
            var applied = ParseAmount(PaymentAmountTextBox.Text);
            var received = ParseAmount(CashReceivedTextBox.Text);
            var change = Math.Max(0m, received - applied);
            ChangeTextBlock.Text = change.ToString("C", _culture);
        }

        private void UpdatePixInformation()
        {
            if (SelectedMethod != "Pix")
                return;
            var amount = ParseAmount(PaymentAmountTextBox.Text);
            _currentPixPayload = null;
            _currentPixReference = null;
            DemoPixNotice.Visibility = Visibility.Collapsed;
            PixQrImage.Visibility = Visibility.Collapsed;
            PixPlaceholder.Visibility = Visibility.Visible;
            CopyPixButton.Visibility = Visibility.Collapsed;

            if (amount <= 0)
            {
                PixAmountTextBlock.Text = "Informe o valor para preparar o PIX.";
                PixPlaceholderText.Text = "O QR aparecerá aqui.";
                return;
            }

            PixAmountTextBlock.Text = $"Cobrança PIX: {amount.ToString("C", _culture)}";
            try
            {
                var isDemo = !PixQrCodeService.IsConfigured;
                var qr = isDemo
                    ? PixQrCodeService.CreateDemo(amount, _saleNumber)
                    : PixQrCodeService.Create(amount, _saleNumber);
                PixQrImage.Source = qr.Image;
                PixQrImage.Visibility = Visibility.Visible;
                PixPlaceholder.Visibility = Visibility.Collapsed;
                CopyPixButton.Visibility = isDemo ? Visibility.Collapsed : Visibility.Visible;
                DemoPixNotice.Visibility = isDemo ? Visibility.Visible : Visibility.Collapsed;
                _currentPixPayload = qr.Payload;
                _currentPixReference = qr.Reference;
            }
            catch (Exception exception)
            {
                PixPlaceholderText.Text = exception.Message;
            }
        }

        private void AddPayment_Click(object sender, RoutedEventArgs e)
        {
            FeedbackTextBlock.Text = string.Empty;
            var amount = ParseAmount(PaymentAmountTextBox.Text);
            if (amount <= 0)
            {
                ShowFeedback("Informe um valor maior que zero para este pagamento.");
                return;
            }
            if (amount > Remaining + 0.009m)
            {
                ShowFeedback($"O valor não pode ultrapassar o restante de {Remaining.ToString("C", _culture)}.");
                return;
            }

            var method = SelectedMethod;
            var received = 0m;
            var change = 0m;
            string? reference = null;
            if (method == "Dinheiro")
            {
                received = ParseAmount(CashReceivedTextBox.Text);
                if (received < amount)
                {
                    ShowFeedback("O valor entregue precisa cobrir esta parte do pagamento.");
                    return;
                }
                change = received - amount;
            }
            else if (method == "Pix")
            {
                if (string.IsNullOrWhiteSpace(_currentPixPayload))
                {
                    ShowFeedback("Não foi possível preparar o QR PIX.");
                    return;
                }
                reference = _currentPixReference;
            }

            PaymentSplits.Add(new PaymentSplit
            {
                Method = method,
                Amount = amount,
                CashReceived = received,
                Change = change,
                Reference = reference
            });
            PaymentAmountTextBox.Clear();
            CashReceivedTextBox.Clear();
            if (Remaining > 0)
                PaymentAmountTextBox.Text = Remaining.ToString("0.00", _culture);
            UpdatePaymentSummary();
            UpdatePixInformation();
            ShowFeedback(IsPaymentComplete
                ? "Pagamento completo. Você já pode finalizar a venda."
                : $"Pagamento adicionado. Falta {Remaining.ToString("C", _culture)}.");
        }

        private void RemovePayment_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: PaymentSplit payment })
            {
                PaymentSplits.Remove(payment);
                UpdatePaymentSummary();
                PaymentAmountTextBox.Text = Remaining > 0m
                    ? Remaining.ToString("0.00", _culture)
                    : string.Empty;
                CashReceivedTextBox.Clear();
                ShowFeedback($"Falta {Remaining.ToString("C", _culture)}.");
            }
        }

        private void CopyPixButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(_currentPixPayload))
                Clipboard.SetText(_currentPixPayload);
        }

        private void UpdatePaymentSummary()
        {
            RemainingTextBlock.Text = Remaining.ToString("C", _culture);
        }

        private decimal ParseAmount(string? text)
        {
            if (decimal.TryParse(text, NumberStyles.Currency, _culture, out var amount))
                return amount;
            return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out amount)
                ? amount
                : 0m;
        }
    }
}
