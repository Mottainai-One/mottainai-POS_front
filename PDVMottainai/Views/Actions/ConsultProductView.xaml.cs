using PDVMottainai.Models;
using PDVMottainai.Services;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace PDVMottainai.Views.Actions
{
    public partial class ConsultProductView : UserControl
    {
        private readonly ObservableCollection<Product> _products = new();

        public ConsultProductView()
        {
            InitializeComponent();
            ProductsList.ItemsSource = _products;
            FilterProducts();
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e) => FilterProducts();

        private void ProductsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProductsList.SelectedItem is not Product product)
            {
                DetailName.Text = "Selecione um produto";
                DetailBrand.Text = "Os detalhes aparecerão aqui.";
                DetailSku.Text = "—";
                DetailBarcode.Text = "—";
                DetailRegularPrice.Text = "—";
                DetailDiscountPrice.Text = "—";
                return;
            }

            DetailName.Text = product.Name;
            DetailBrand.Text = string.IsNullOrWhiteSpace(product.Brand) ? "Produto Mottainai" : product.Brand!;
            DetailSku.Text = product.Sku;
            DetailBarcode.Text = product.Barcode;
            var culture = System.Globalization.CultureInfo.GetCultureInfo("pt-BR");
            DetailRegularPrice.Text = product.RegularPrice?.ToString("C", culture) ?? "Não informado";
            DetailDiscountPrice.Text = product.DiscountPrice?.ToString("C", culture) ?? "Sem promoção cadastrada";
        }

        private void FilterProducts()
        {
            var query = SearchTextBox?.Text?.Trim() ?? string.Empty;
            _products.Clear();
            foreach (var product in MockApiService.Products.Where(product => product.Active &&
                (query.Length == 0 || product.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                 product.Sku.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                 product.Barcode.Contains(query, StringComparison.OrdinalIgnoreCase))))
                _products.Add(product);
            CountText.Text = $"{_products.Count} produto(s)";
        }
    }
}
