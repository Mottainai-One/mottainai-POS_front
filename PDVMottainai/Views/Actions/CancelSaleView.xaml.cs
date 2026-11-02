using System.Windows.Controls;

namespace PDVMottainai.Views.Actions
{
    public partial class CancelSaleView : UserControl
    {
        public CancelSaleView(bool hasItems = true)
        {
            InitializeComponent();
            if (!hasItems)
            {
                HeadingText.Text = "Venda vazia";
                DescriptionText.Text = "Não há produtos bipados para cancelar.";
                ActionNoteText.Text = "Bipe um produto para começar a venda.";
            }
        }
    }
}
