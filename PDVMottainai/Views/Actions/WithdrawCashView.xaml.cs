using System.Windows.Controls;

namespace PDVMottainai.Views.Actions
{
    public partial class WithdrawCashView : UserControl
    {
        public string AmountText => AmountTextBox.Text;
        public string Reason => ReasonTextBox.Text.Trim();
        public void ShowFeedback(string message) => FeedbackText.Text = message;

        public WithdrawCashView(string currentBalance)
        {
            InitializeComponent();
            BalanceText.Text = $"Saldo registrado: {currentBalance}";
        }
    }
}
