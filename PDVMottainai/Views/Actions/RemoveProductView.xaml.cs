using PDVMottainai.Models;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace PDVMottainai.Views.Actions
{
    public partial class RemoveProductView : UserControl
    {
        public ScannedProductLine? SelectedLine => SaleItemsList.SelectedItem as ScannedProductLine;
        public void ShowFeedback(string message) => FeedbackText.Text = message;

        public RemoveProductView(ObservableCollection<ScannedProductLine> items)
        {
            InitializeComponent();
            SaleItemsList.ItemsSource = items;
        }
    }
}
