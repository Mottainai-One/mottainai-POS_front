using System.Windows.Controls;

namespace PDVMottainai.Views.Actions
{
    public partial class StartSaleView : UserControl
    {
        public StartSaleView(bool hasCurrentItems = false)
        {
            InitializeComponent();
            if (hasCurrentItems)
                DescriptionText.Text = "Iniciar uma nova venda removerá os itens que ainda estão na venda atual.";
        }
    }
}
