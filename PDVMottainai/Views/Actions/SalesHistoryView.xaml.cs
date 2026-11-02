using PDVMottainai.Models;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace PDVMottainai.Views.Actions
{
    public partial class SalesHistoryView : UserControl
    {
        private readonly CultureInfo _culture = CultureInfo.GetCultureInfo("pt-BR");

        public SalesHistoryView(IEnumerable<CompletedSale> history)
        {
            InitializeComponent();
            var entries = history.OrderByDescending(sale => sale.SaleDate).ToList();
            HistoryList.ItemsSource = entries;
            CountTextBlock.Text = $"{entries.Count} venda(s)";
            EmptyStateText.Visibility = entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            if (entries.Count > 0)
                HistoryList.SelectedIndex = 0;
        }

        private void HistoryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (HistoryList.SelectedItem is not CompletedSale sale)
                return;

            DetailCustomer.Text = sale.CustomerName;
            DetailDate.Text = $"Venda #{sale.SaleNumber} · {sale.SaleDate:dd/MM/yyyy às HH:mm}";
            DetailCpf.Text = string.IsNullOrWhiteSpace(sale.CustomerCpf) ? "CPF não informado" :
                $"CPF: {sale.CustomerCpf}" + (sale.IsMottainaiMember ? " · cliente Mottainai" : " · cliente comum");
            DetailRegularTotal.Text = sale.RegularTotal.ToString("C", _culture);
            DetailDiscountTotal.Text = sale.DiscountTotal.ToString("C", _culture);
            DetailEarned.Text = sale.Earned.ToString("C", _culture);
            DetailItemsList.ItemsSource = sale.Items;
            PaymentDetailsText.Text = "Pagamentos: " +
                (sale.Payments.Count == 0
                    ? "—"
                    : string.Join(" · ", sale.Payments.Select(payment =>
                        payment.Change > 0
                            ? $"{payment.Method} {payment.Amount.ToString("C", _culture)} (troco {payment.Change.ToString("C", _culture)})"
                            : $"{payment.Method} {payment.Amount.ToString("C", _culture)}")));
            InvoiceText.Text = sale.CpfOnInvoice
                ? $"CPF na nota: {sale.CustomerCpf}"
                : "CPF na nota: não solicitado";
        }
    }
}
