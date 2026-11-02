using PDVMottainai.Models;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PDVMottainai.Views.Actions
{
    public partial class FinalizeShiftView : UserControl
    {
        private readonly CultureInfo _culture = CultureInfo.GetCultureInfo("pt-BR");

        public FinalizeShiftView(ShiftClosingSummary summary)
        {
            InitializeComponent();
            SummaryPanel.Visibility = Visibility.Visible;
            ClosedShiftPanel.Visibility = Visibility.Collapsed;

            EmployeeText.Text = $"{summary.EmployeeName} · {summary.StoreName}";
            ShiftPeriodText.Text = $"Início: {summary.StartedAt:dd/MM/yyyy HH:mm}   ·   Fechamento: {summary.ClosedAt:dd/MM/yyyy HH:mm}";
            SalesCountText.Text = summary.SalesCount.ToString("N0", _culture);
            ItemsSoldText.Text = summary.ItemsSold.ToString("N0", _culture);
            AverageTicketText.Text = summary.AverageTicket.ToString("C", _culture);
            DurationText.Text = FormatDuration(summary.Duration);

            DebitTotalText.Text = summary.DebitTotal.ToString("C", _culture);
            CreditTotalText.Text = summary.CreditTotal.ToString("C", _culture);
            PixTotalText.Text = summary.PixTotal.ToString("C", _culture);
            CashSalesTotalText.Text = summary.CashSalesTotal.ToString("C", _culture);
            RegularTotalText.Text = summary.RegularTotal.ToString("C", _culture);
            PaidTotalText.Text = summary.PaidTotal.ToString("C", _culture);
            PromotionSavingsText.Text = summary.PromotionSavings.ToString("C", _culture);

            CountedCashText.Text = summary.CountedCash.ToString("C", _culture);
            ExpectedCashText.Text = summary.ExpectedCash.ToString("C", _culture);
            CashAddedText.Text = summary.CashAdded.ToString("C", _culture);
            CashWithdrawnText.Text = summary.CashWithdrawn.ToString("C", _culture);
            CashDifferenceText.Text = summary.CashDifference == 0m
                ? "Caixa conferido · sem diferença"
                : summary.CashDifference < 0m
                    ? $"Falta {Math.Abs(summary.CashDifference).ToString("C", _culture)}"
                    : $"Sobra {summary.CashDifference.ToString("C", _culture)}";
            CashDifferenceText.Foreground = summary.CashDifference == 0m
                ? new SolidColorBrush(Color.FromRgb(24, 115, 68))
                : new SolidColorBrush(Color.FromRgb(180, 35, 24));

            BestSellingProductText.Text = summary.BestSellingProduct;
            BestSellingQuantityText.Text = $"{summary.BestSellingQuantity.ToString("N0", _culture)} unidade(s)";
            BestSellingRevenueText.Text = summary.BestSellingRevenue.ToString("C", _culture);
        }

        public FinalizeShiftView(int salesCount, int pendingItems, string cashBalance)
        {
            InitializeComponent();
            SummaryPanel.Visibility = Visibility.Collapsed;
            ClosedShiftPanel.Visibility = Visibility.Visible;
            ClosedStateTitleText.Text = pendingItems > 0 ? "Venda ainda em andamento" : "Turno encerrado";
            ClosedStateDescriptionText.Text = pendingItems > 0
                ? $"Conclua ou cancele os {pendingItems} item(ns) pendentes antes de fechar o turno."
                : "Este turno já foi encerrado e não aceita novas vendas.";
        }

        private static string FormatDuration(TimeSpan duration)
        {
            var totalHours = (int)duration.TotalHours;
            return $"{totalHours:D2}h {duration.Minutes:D2}min";
        }
    }
}