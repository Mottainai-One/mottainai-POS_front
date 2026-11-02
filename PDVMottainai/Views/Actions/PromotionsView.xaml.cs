using PDVMottainai.Models;
using System.Collections.Generic;
using System.Windows.Controls;

namespace PDVMottainai.Views.Actions
{
    public partial class PromotionsView : UserControl
    {
        public PromotionsView(IEnumerable<Product> products)
        {
            InitializeComponent();
            PromotionsList.ItemsSource = products;
            EmptyStateText.Visibility = products.Any() ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
        }
    }
}
