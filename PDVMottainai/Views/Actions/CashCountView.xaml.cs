using System.Globalization;
using System.Windows.Controls;

namespace PDVMottainai.Views.Actions
{
    public partial class CashCountView : UserControl
    {
        private readonly CultureInfo _culture = CultureInfo.GetCultureInfo("pt-BR");

        public decimal CountedCash { get; private set; }

        public CashCountView()
        {
            InitializeComponent();
        }

        public bool TryReadCountedCash(out decimal amount)
        {
            if (decimal.TryParse(CountedCashTextBox.Text, NumberStyles.Currency, _culture, out amount) ||
                decimal.TryParse(CountedCashTextBox.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out amount))
            {
                if (amount >= 0m)
                {
                    CountedCash = amount;
                    return true;
                }
            }

            FeedbackTextBlock.Text = "Informe um valor válido igual ou maior que zero, por exemplo 350,00.";
            CountedCashTextBox.Focus();
            CountedCashTextBox.SelectAll();
            return false;
        }
    }
}