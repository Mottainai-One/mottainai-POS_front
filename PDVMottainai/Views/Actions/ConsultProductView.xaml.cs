using PDVMottainai.Models;
using PDVMottainai.Services;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;

namespace PDVMottainai.Views.Actions
{
    public partial class ConsultProductView : UserControl
    {
        private readonly ObservableCollection<Product> _products = new();
        private readonly PosApiClient _api;

        public ConsultProductView(PosApiClient api)
        {
            _api = api;
            InitializeComponent();
            ProductsList.ItemsSource = _products;
            Loaded += async (_, _) => await FilterProductsAsync();
        }

        private async void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e) => await FilterProductsAsync();

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

        private async Task FilterProductsAsync()
        {
            var query = SearchTextBox?.Text?.Trim() ?? string.Empty;
            try
            {
                var result = await _api.GetProductsAsync(query);
                _products.Clear();
                foreach (var item in result)
                    _products.Add(new Product
                    {
                        ProductId = item.ProductId,
                        Sku = item.Sku,
                        Barcode = item.Barcode ?? string.Empty,
                        Name = item.Name,
                        UnitMeasure = item.UnitMeasure,
                        RegularPrice = item.NormalPrice,
                        DiscountPrice = item.PromotionalPrice,
                        Active = item.Active,
                        Version = item.Version
                    });
                CountText.Text = $"{_products.Count} product(s)";
            }
            catch (Exception exception) when (exception is PosApiException or HttpRequestException)
            {
                MessageBox.Show($"Could not search products: {exception.Message}", "POS API", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
