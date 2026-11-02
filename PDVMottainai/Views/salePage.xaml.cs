using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using PDVMottainai.Services;
using PDVMottainai.Models;
using PDVMottainai.Views.Actions;


namespace PDVMottainai.Views
{
    /// <summary>
    /// Lógica interna para salePage.xaml
    /// </summary>
    public partial class salePage : Page
    {
        private DispatcherTimer? _timer;
        private DispatcherTimer? _connectivityTimer;
        private bool _connectivityCheckRunning;
        private readonly DateTime _shiftStartedAt = DateTime.Now;
        private readonly PosApiClient _api;
        private PDVMottainai.Services.PosShift? _shift;
        private int? _currentSaleId;
        private readonly Dictionary<int, int> _saleItemIds = new();
        private Employee? _currentEmployee;
        private string _storeName = "Não informada";
        private readonly List<CompletedSale> _salesHistory = new();
        private readonly List<string> _cashMovements = new();
        private decimal _cashBalance;
        private decimal _cashAddedTotal;
        private decimal _cashWithdrawnTotal;
        private bool _shiftClosed;
        private bool _customerIsMottainai;
        private bool _customerWantsCpfInvoice;
        private bool _customerDecisionMade;
        private bool _isAddingProduct;
        private string _currentCustomerName = "Consumidor não identificado";
        private string _currentCustomerCpf = string.Empty;
        private int _saleNumber = 1;

        private enum SaleCustomerPromptResult
        {
            Cancelled,
            Continued
        }

        public ObservableCollection<ScannedProductLine> ScannedProducts { get; } = new();

        public salePage(PosApiClient api, PosSession session)
        {
            var operatorProfile = session.OperatorProfile ?? throw new ArgumentException("The API session does not include an operator profile.", nameof(session));
            _api = api;
            _storeName = operatorProfile.StoreName;
            _currentEmployee = new Employee
            {
                EmployeeId = operatorProfile.EmployeeId,
                StoreId = operatorProfile.StoreId,
                Name = operatorProfile.Name
            };
            InitializeComponent();
            SetSaleStatus("Venda em andamento");
            DataContext = this;
            scannedProductsList.ItemsSource = ScannedProducts;
            UpdateSaleTotals();
            ChangeDateTime();
            txtStore.Text = $"Loja: {_storeName}";
            txtNumSale.Text = "—";
            Loaded += async (_, _) => await InitializePosAsync();
        }

        private async Task InitializePosAsync()
        {
            try
            {
                _shift = await _api.GetCurrentShiftAsync();
                if (_shift is null)
                {
                    var countView = new CashCountView();
                    var confirmed = ShowActionDialog("Open shift", "Count the starting cash before opening this terminal's shift.",
                        "Open shift", countView, () => countView.TryReadCountedCash(out _));
                    if (!confirmed)
                    {
                        formBarcode.IsEnabled = false;
                        return;
                    }

                    var opened = await _api.OpenShiftAsync(countView.CountedCash);
                    _shift = new PDVMottainai.Services.PosShift
                    {
                        ShiftId = opened.GetProperty("shiftId").GetInt32(),
                        Status = "OPEN",
                        OpenedAt = DateTimeOffset.Now,
                        OpeningAmount = countView.CountedCash
                    };
                }

                _cashBalance = _shift.OpeningAmount;
            }
            catch (Exception exception) when (exception is PosApiException or HttpRequestException)
            {
                formBarcode.IsEnabled = false;
                MessageBox.Show($"Could not initialize the POS shift: {exception.Message}", "POS API", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        public void ChangeDateTime()
        {
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (_, _) => UpdateCurrentDateTime();
            UpdateCurrentDateTime();
            _timer.Start();
        }// Define o intervalo de 1 segundo

        private void UpdateCurrentDateTime()
        {
            var now = DateTime.Now;
            txtCurrentTime.Text = now.ToString("HH:mm:ss");
            txtCurrentDate.Text = now.ToString("dd/MM/yyyy");
        }

        //}
        //public void ChangeDateTime()
        //{
        //    // Inicializa o temporizador
        //    _timer = new DispatcherTimer();
        //    _timer.Interval = TimeSpan.FromSeconds(1); // Define o intervalo de 1 segundo

        //    // Associa o evento que roda a cada tique do relógio
        //    _timer.Tick += (sender, e) =>
        //    {
        //        txtDateTime.Text = $"{DateTime.Now:dd/MM/yyyy} | {DateTime.Now:HH:mm:ss}";
        //    };

        private void formBarcode_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (formBarcode.Text.Length == 13)
            {
                AddProductFromBarcode();
            }
        }

        private void formBarcode_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                AddProductFromBarcode();
                e.Handled = true;
            }
        }

        private void SearchBarcode_Click(object sender, RoutedEventArgs e)
        {
            AddProductFromBarcode();
        }

        private void CopyBarcode_Click(object sender, RoutedEventArgs e)
        {
            CopyProductValue(txtBarCode.Text);
        }

        private void CopySku_Click(object sender, RoutedEventArgs e)
        {
            CopyProductValue(txtSkuProduct.Text);
        }

        private static void CopyProductValue(string value)
        {
            if (!string.IsNullOrWhiteSpace(value) && value != "-")
                Clipboard.SetText(value.Trim());
        }

        private async void AddProductFromBarcode()
        {
            var barcode = formBarcode.Text.Trim();
            if (string.IsNullOrWhiteSpace(barcode) || _isAddingProduct)
                return;

            _isAddingProduct = true;
            try
            {
                if (ScannedProducts.Count == 0 && !_customerDecisionMade)
                {
                    var customerPrompt = RequestSaleCustomer("Customer lookup is unavailable in this POS build. Continue without customer identification.");
                    if (customerPrompt == SaleCustomerPromptResult.Cancelled)
                    {
                        formBarcode.SelectAll();
                        formBarcode.Focus();
                        return;
                    }
                    SetSaleStatus("Sale in progress");
                }

                if (_currentSaleId is null)
                    await BeginDraftSaleAsync();

                var products = await _api.GetProductsAsync(barcode);
                var apiProduct = products.FirstOrDefault(product =>
                    string.Equals(product.Barcode, barcode, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(product.Sku, barcode, StringComparison.OrdinalIgnoreCase));
                if (apiProduct is null)
                {
                    txtProductName.Text = "-";
                    txtBarCode.Text = "-";
                    txtSkuProduct.Text = "-";
                    MessageBox.Show("Product not found. Check the barcode and try again.", "Product not found",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    formBarcode.SelectAll();
                    formBarcode.Focus();
                    return;
                }

                var item = await _api.AddSaleItemAsync(_currentSaleId!.Value, apiProduct.ProductId, 1);
                var itemId = item.GetProperty("itemId").GetInt32();
                _saleItemIds[apiProduct.ProductId] = itemId;
                var unitPrice = item.GetProperty("unitPrice").GetDecimal();
                var productFound = new Product
                {
                    ProductId = apiProduct.ProductId,
                    Sku = apiProduct.Sku,
                    Barcode = apiProduct.Barcode ?? string.Empty,
                    Name = apiProduct.Name,
                    UnitMeasure = apiProduct.UnitMeasure,
                    RegularPrice = unitPrice,
                    DiscountPrice = unitPrice,
                    Active = apiProduct.Active,
                    Version = apiProduct.Version
                };

                txtProductName.Text = productFound.Name;
                txtBarCode.Text = productFound.Barcode;
                txtSkuProduct.Text = productFound.Sku;

                var existingLine = ScannedProducts.FirstOrDefault(line => line.Product.ProductId == productFound.ProductId);
                if (existingLine is null)
                    ScannedProducts.Add(new ScannedProductLine(productFound));
                else
                    existingLine.Quantity++;

                UpdateSaleTotals();
                formBarcode.Clear();
                formBarcode.Focus();
            }
            catch (Exception exception) when (exception is PosApiException or HttpRequestException)
            {
                MessageBox.Show($"Could not add the item: {exception.Message}", "POS API", MessageBoxButton.OK, MessageBoxImage.Error);
                formBarcode.SelectAll();
                formBarcode.Focus();
            }
            finally
            {
                _isAddingProduct = false;
            }
        }

        private async Task BeginDraftSaleAsync()
        {
            if (_currentSaleId is not null)
                return;
            var sale = await _api.CreateSaleAsync(null);
            _currentSaleId = sale.GetProperty("saleId").GetInt32();
            _saleNumber = _currentSaleId.Value;
            _saleItemIds.Clear();
            txtNumSale.Text = _saleNumber.ToString();
        }

        private (decimal Regular, decimal Discount, decimal Earned) CalculateSaleTotals()
        {
            var regular = ScannedProducts.Sum(line => line.RegularUnitPrice * line.Quantity);
            var discount = ScannedProducts.Sum(line => line.AppliedUnitPrice * line.Quantity);
            return (regular, discount, Math.Max(0m, regular - discount));
        }

        private void UpdateSaleTotals()
        {
            if (txtSecondaryValue == null)
                return;

            var totals = CalculateSaleTotals();
            var culture = CultureInfo.GetCultureInfo("pt-BR");
            if (_customerIsMottainai)
            {
                txtSecondaryLabel.Text = "Valor total com desconto";
                txtSecondaryValue.Text = totals.Discount.ToString("C", culture);
                txtHeroLabel.Text = "Valor total sem desconto";
                txtHeroValue.Text = totals.Regular.ToString("C", culture);
            }
            else
            {
                txtSecondaryLabel.Text = "Valor total sem desconto";
                txtSecondaryValue.Text = totals.Regular.ToString("C", culture);
                txtHeroLabel.Text = "Valor total com desconto";
                txtHeroValue.Text = totals.Discount.ToString("C", culture);
            }

            txtGainLabel.Text = _customerIsMottainai ? "Valor ganho em promoções" : "Economia";
            txtGainValue.Text = totals.Earned.ToString("C", culture);
            foreach (var line in ScannedProducts)
                line.IsMottainaiMember = _customerIsMottainai;
        }

        private static string FormatCpf(string cpf) =>
            cpf.Length == 11 ? $"{cpf[..3]}.{cpf[3..6]}.{cpf[6..9]}-{cpf[9..]}" : cpf;

        private void UpdateCustomerCard()
        {
            txtNumSa.Text = _currentCustomerName;
            txtNumSal.Text = string.IsNullOrWhiteSpace(_currentCustomerCpf) ? "Não informado" : _currentCustomerCpf;
            txtNumS.Text = _customerIsMottainai ? "Nível sustentável" : "Não identificado";
        }

        private void SetSaleStatus(string status)
        {
            txtNum.Text = status;
            var colors = status switch
            {
                "Venda finalizada" => (Card: "#E8F5EC", Text: "#187344"),
                "Venda cancelada" => (Card: "#FDECEC", Text: "#B42318"),
                _ => (Card: "#E6F0FA", Text: "#2864A4")
            };
            var iconFile = status switch
            {
                "Venda finalizada" => "inProgressIcon.png",
                "Venda cancelada" => "cancelSaleIcon.png",
                _ => "openSaleIcon.png"
            };
            saleStatusCard.Background = (Brush)new BrushConverter().ConvertFromString(colors.Card)!;
            txtNum.Foreground = (Brush)new BrushConverter().ConvertFromString(colors.Text)!;
            saleStatusIcon.Source = new BitmapImage(new Uri($"pack://application:,,,/IMG/{iconFile}", UriKind.Absolute));
        }

        private SaleCustomerPromptResult RequestSaleCustomer(string subtitle)
        {
            var customerView = new SaleCustomerView();
            var continueWithoutCpf = false;
            var dialog = new ActionDialogWindow("Cliente e nota fiscal", subtitle,
                "Continuar venda", customerView, height: 640, cancelText: "Continuar sem CPF")
            {
                Owner = Window.GetWindow(this),
                ConfirmAction = () =>
                {
                    customerView.ResolveCustomer();
                    if (customerView.HasInvalidCpf)
                    {
                        if (customerView.WantsCpfOnInvoice)
                        {
                            customerView.ShowFeedback("Para incluir o CPF na nota, informe os 11 números. Caso contrário, marque ‘Não’ ou continue sem CPF.");
                            return false;
                        }
                        customerView.DiscardIncompleteCpf();
                    }
                    if (customerView.WantsCpfOnInvoice && customerView.CustomerCpf.Length != 11)
                    {
                        customerView.ShowFeedback("Informe um CPF válido para incluir na nota fiscal.");
                        return false;
                    }

                    _customerIsMottainai = customerView.IsMottainaiMember;
                    _customerWantsCpfInvoice = customerView.WantsCpfOnInvoice;
                    _currentCustomerName = customerView.CustomerName;
                    _currentCustomerCpf = customerView.CustomerCpf.Length == 11
                        ? FormatCpf(customerView.CustomerCpf)
                        : string.Empty;
                    _customerDecisionMade = true;
                    UpdateCustomerCard();
                    UpdateSaleTotals();
                    return true;
                },
                CancelAction = () =>
                {
                    continueWithoutCpf = true;
                    _customerIsMottainai = false;
                    _customerWantsCpfInvoice = false;
                    _currentCustomerName = "Consumidor não identificado";
                    _currentCustomerCpf = string.Empty;
                    _customerDecisionMade = true;
                    UpdateCustomerCard();
                    UpdateSaleTotals();
                }
            };

            var confirmed = dialog.ShowDialog() == true;
            if (confirmed || continueWithoutCpf)
            {
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, () =>
                {
                    formBarcode.Focus();
                    Keyboard.Focus(formBarcode);
                });
                return SaleCustomerPromptResult.Continued;
            }

            return SaleCustomerPromptResult.Cancelled;
        }

        private void SalePage_Loaded(object sender, RoutedEventArgs e)
        {
            if (_timer is { IsEnabled: false }) _timer.Start();
            StartConnectivityMonitoring();
            formBarcode.Focus();
        }

        private void SalePage_Unloaded(object sender, RoutedEventArgs e)
        {
            _timer?.Stop();
            _connectivityTimer?.Stop();
        }

        private void StartConnectivityMonitoring()
        {
            _connectivityTimer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
            _connectivityTimer.Tick -= ConnectivityTimer_Tick;
            _connectivityTimer.Tick += ConnectivityTimer_Tick;
            _connectivityTimer.Start();
            _ = RefreshConnectivityStatusAsync();
        }

        private async void ConnectivityTimer_Tick(object? sender, EventArgs e) =>
            await RefreshConnectivityStatusAsync();

        private async Task RefreshConnectivityStatusAsync()
        {
            if (_connectivityCheckRunning || !IsLoaded)
                return;

            _connectivityCheckRunning = true;
            try
            {
                var isOnline = await InternetConnectivityService.HasInternetAccessAsync();
                if (!IsLoaded) return;

                if (isOnline)
                {
                    ConnectionTitle.Text = "Internet conectada";
                    ConnectionTitle.Foreground = new SolidColorBrush(Color.FromRgb(24, 115, 68));
                    ConnectionSubtitle.Text = "O caixa tem acesso à internet.";
                    ConnectionIndicator.Fill = new SolidColorBrush(Color.FromRgb(46, 160, 93));
                }
                else
                {
                    ConnectionTitle.Text = "Sem conexão com a internet";
                    ConnectionTitle.Foreground = new SolidColorBrush(Color.FromRgb(180, 35, 24));
                    ConnectionSubtitle.Text = "Verifique a rede. O caixa pode estar sem acesso a serviços online.";
                    ConnectionIndicator.Fill = new SolidColorBrush(Color.FromRgb(196, 69, 54));
                }
            }
            finally
            {
                _connectivityCheckRunning = false;
            }
        }

        private void SalePage_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (e.Key == Key.B)
                {
                    CopyProductValue(txtBarCode.Text);
                    e.Handled = true;
                    return;
                }
                if (e.Key == Key.S)
                {
                    CopyProductValue(txtSkuProduct.Text);
                    e.Handled = true;
                    return;
                }
            }

            var action = e.Key switch
            {
                Key.F1 => "StartSale",
                Key.F2 => "CancelSale",
                Key.F3 => "ConsultProduct",
                Key.F4 => "FinalizeSale",
                Key.F5 => "RemoveProduct",
                Key.F6 => "SalesHistory",
                Key.F7 => "Promotions",
                Key.F8 => "EmployeeProfile",
                Key.F9 => "FindCustomer",
                Key.F10 => "WithdrawCash",
                Key.F11 => "AddCash",
                Key.F12 => "FinalizeShift",
                _ => null
            };

            if (action == null)
                return;

            e.Handled = true;
            ExecuteFooterAction(action);
        }

        private void FooterAction_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: string action })
                ExecuteFooterAction(action);
        }

        private void ExecuteFooterAction(string action)
        {
            switch (action)
            {
                case "StartSale":
                    StartSale();
                    break;
                case "CancelSale":
                    CancelSale();
                    break;
                case "ConsultProduct":
                    ShowActionDialog("Consultar produto", "Pesquise e selecione um produto para ver seus detalhes.",
                        "Concluir", new ConsultProductView(_api), fullScreen: true);
                    break;
                case "FinalizeSale":
                    FinalizeSale();
                    break;
                case "RemoveProduct":
                    RemoveSelectedProduct();
                    break;
                case "SalesHistory":
                    ShowSalesHistory();
                    break;
                case "Promotions":
                    ShowPromotions();
                    break;
                case "FinalizeShift":
                    FinalizeShift();
                    break;
                case "FindCustomer":
                    FindCustomer();
                    break;
                case "WithdrawCash":
                    RegisterCashMovement(isAddition: false);
                    break;
                case "AddCash":
                    RegisterCashMovement(isAddition: true);
                    break;
                case "EmployeeProfile":
                    ShowEmployeeProfile();
                    break;
            }
        }

        private void ShowEmployeeProfile()
        {
            var profile = new EmployeeProfileView(_currentEmployee, _storeName, _salesHistory.Count, _shiftStartedAt);
            ShowActionDialog("Perfil do funcionário", "Resumo pessoal e atividade deste turno.",
                "Fechar perfil", profile, fullScreen: true);
        }

        private async void StartSale()
        {
            if (_shiftClosed)
            {
                ShowActionDialog("Turno encerrado", "Não é possível iniciar outra venda neste turno.",
                    "Fechar", new FinalizeShiftView(_salesHistory.Count, 0,
                        _cashBalance.ToString("C", CultureInfo.GetCultureInfo("pt-BR"))));
                return;
            }

            if (_currentSaleId is not null)
            {
                MessageBox.Show("This API does not provide a sale completion or cancellation route. The current draft must remain open until an authorized workflow is available.",
                    "Draft sale still open", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var startView = new StartSaleView(ScannedProducts.Count > 0);
            if (!ShowActionDialog("Iniciar venda", "Abra uma nova venda no terminal.",
                    "Continuar", startView))
                return;

            if (RequestSaleCustomer("Customer lookup is unavailable in this POS build. Continue without customer identification.")
                == SaleCustomerPromptResult.Cancelled)
                return;

            ScannedProducts.Clear();
            _currentSaleId = null;
            _saleItemIds.Clear();
            txtProductName.Text = "-";
            txtBarCode.Text = "-";
            txtSkuProduct.Text = "-";
            SetSaleStatus("Venda em andamento");
            txtNumSale.Text = _saleNumber.ToString();
            UpdateCustomerCard();
            formBarcode.Clear();
            UpdateSaleTotals();

            try
            {
                await BeginDraftSaleAsync();
            }
            catch (Exception exception) when (exception is PosApiException or HttpRequestException)
            {
                MessageBox.Show($"Could not start the sale: {exception.Message}", "POS API", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // ShowDialog desativa a janela principal. Só devolva o foco ao leitor
            // depois que o popup fechar; focar dentro do callback deixa o PDV sem cursor.
            await Dispatcher.InvokeAsync(() =>
            {
                formBarcode.Focus();
                Keyboard.Focus(formBarcode);
            }, System.Windows.Threading.DispatcherPriority.Input);
        }

        private async void CancelSale()
        {
            var canCancel = ScannedProducts.Count > 0;
            if (!canCancel || _currentSaleId is null)
            {
                MessageBox.Show("There is no active sale to cancel.", "Cancel sale", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (MessageBox.Show("The API records cancellation requests for manager approval. The draft will remain open until it is approved.",
                    "Request sale cancellation", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK)
                return;

            try
            {
                var response = await _api.CreateApprovalAsync(new
                {
                    saleId = _currentSaleId.Value,
                    saleItemId = (int?)null,
                    requestType = "CANCEL_PURCHASE",
                    reason = "Cancellation requested by the operator.",
                    requestedAmount = (decimal?)null,
                    discountPercent = (decimal?)null,
                    idempotencyKey = Guid.NewGuid()
                });
                MessageBox.Show($"Cancellation request {response.GetProperty("requestId").GetString()} was submitted for approval. The sale remains open.",
                    "Approval requested", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception exception) when (exception is PosApiException or HttpRequestException)
            {
                MessageBox.Show($"Could not request cancellation: {exception.Message}", "POS API", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void FinalizeSale()
        {
            var totals = CalculateSaleTotals();
            var view = new FinalizeSaleView(ScannedProducts, totals.Discount, _currentCustomerName,
                _currentCustomerCpf, _customerWantsCpfInvoice, _saleNumber);
            var hasItems = ScannedProducts.Count > 0;
            if (!ShowActionDialog("Simulate checkout",
                    hasItems ? "The API only simulates payment and will keep this sale as a draft." : "Add products before checkout.",
                    hasItems ? "Simulate payment" : "Return to sale", view, fullScreen: true))
                return;

            if (!hasItems || _currentSaleId is null)
                return;
            if (!view.IsPaymentComplete)
            {
                MessageBox.Show($"Add payments to cover {view.Remaining.ToString("C", CultureInfo.GetCultureInfo("pt-BR"))}.", "Payment required", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var payments = view.GetPaymentRecords().Select(payment => new
                {
                    paymentMethod = NormalizeForComparison(payment.Method) switch
                    {
                        "DINHEIRO" => "CASH",
                        "PIX" => "PIX",
                        "CARTAO DE DEBITO" => "DEBIT_CARD",
                        "CARTAO DE CREDITO" => "CREDIT_CARD",
                        _ => payment.Method
                    },
                    payment.Amount
                }).ToArray();
                var simulation = await _api.SimulateSaleCompletionAsync(_currentSaleId.Value, new { payments });
                MessageBox.Show($"API simulation succeeded for {simulation.GetProperty("totalAmount").GetDecimal().ToString("C", CultureInfo.GetCultureInfo("pt-BR"))}. The sale is still a draft; no payment was processed.",
                    "Checkout simulation", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception exception) when (exception is PosApiException or HttpRequestException)
            {
                MessageBox.Show($"Could not simulate checkout: {exception.Message}", "POS API", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void RemoveSelectedProduct()
        {
            var view = new RemoveProductView(ScannedProducts);
            var hasItems = ScannedProducts.Count > 0;
            if (!ShowActionDialog("Remove product", hasItems ? "Select an item to reduce its quantity." : "There are no items to remove.",
                    hasItems ? "Remove one unit" : "Close", view))
                return;
            if (!hasItems || view.SelectedLine is not { } selectedLine || _currentSaleId is null)
                return;

            try
            {
                if (selectedLine.Quantity > 1)
                {
                    var newQuantity = selectedLine.Quantity - 1;
                    await _api.ChangeSaleItemAsync(_currentSaleId.Value, _saleItemIds[selectedLine.Product.ProductId], newQuantity);
                    selectedLine.Quantity = newQuantity;
                    UpdateSaleTotals();
                    return;
                }

                var approval = await _api.CreateApprovalAsync(new
                {
                    saleId = _currentSaleId.Value,
                    saleItemId = _saleItemIds[selectedLine.Product.ProductId],
                    requestType = "CANCEL_PRODUCT",
                    reason = "Item removal requested by the operator.",
                    requestedAmount = (decimal?)null,
                    discountPercent = (decimal?)null,
                    idempotencyKey = Guid.NewGuid()
                });
                MessageBox.Show($"Removal request {approval.GetProperty("requestId").GetString()} was submitted for manager approval. The item remains in the draft.",
                    "Approval requested", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception exception) when (exception is PosApiException or HttpRequestException or KeyNotFoundException)
            {
                MessageBox.Show($"Could not update the sale item: {exception.Message}", "POS API", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ShowSalesHistory()
        {
            try
            {
                var response = await _api.GetSalesAsync();
                var history = response.EnumerateArray().Select(sale => new CompletedSale
                {
                    SaleNumber = sale.GetProperty("saleId").GetInt32(),
                    SaleDate = DateTime.TryParse(sale.GetProperty("saleDate").GetString(), out var date) ? date : DateTime.MinValue,
                    Status = sale.TryGetProperty("status", out var status) ? status.GetString() ?? string.Empty : string.Empty,
                    CustomerName = "Registro da API",
                    DiscountTotal = sale.TryGetProperty("totalAmount", out var total) && total.ValueKind == JsonValueKind.Number ? total.GetDecimal() : 0m,
                    RegularTotal = sale.TryGetProperty("totalAmount", out total) && total.ValueKind == JsonValueKind.Number ? total.GetDecimal() : 0m
                }).ToList();
                ShowActionDialog("Sales from POS API", "The API currently returns sale summary records.",
                    "Close", new SalesHistoryView(history), fullScreen: true);
            }
            catch (Exception exception) when (exception is PosApiException or HttpRequestException)
            {
                MessageBox.Show($"Could not load sales: {exception.Message}", "POS API", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ShowPromotions()
        {
            try
            {
                var response = await _api.GetPromotionsAsync();
                var promotions = response.EnumerateArray().Select(promotion => new Product
                {
                    ProductId = promotion.GetProperty("productId").GetInt32(),
                    Name = promotion.GetProperty("productName").GetString() ?? string.Empty,
                    Sku = promotion.GetProperty("sku").GetString() ?? string.Empty,
                    RegularPrice = promotion.GetProperty("normalPrice").GetDecimal(),
                    DiscountPrice = promotion.GetProperty("promotionalPrice").GetDecimal()
                }).ToList();
                ShowActionDialog("Promotions", "Active promotions from the POS API.",
                    "Close", new PromotionsView(promotions), fullScreen: true);
            }
            catch (Exception exception) when (exception is PosApiException or HttpRequestException)
            {
                MessageBox.Show($"Could not load promotions: {exception.Message}", "POS API", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void FinalizeShift()
        {
            if (_shiftClosed)
            {
                ShowActionDialog("Turno encerrado", "Este turno já foi finalizado.", "Fechar",
                    new FinalizeShiftView(_salesHistory.Count, 0, _cashBalance.ToString("C", CultureInfo.GetCultureInfo("pt-BR"))));
                return;
            }

            var pendingItems = ScannedProducts.Sum(line => line.Quantity);
            if (pendingItems > 0)
            {
                ShowActionDialog("Finalizar turno", "Conclua a venda atual antes de fechar o turno.", "Voltar",
                    new FinalizeShiftView(_salesHistory.Count, pendingItems,
                        _cashBalance.ToString("C", CultureInfo.GetCultureInfo("pt-BR"))));
                return;
            }

            if (_shift is null)
                return;

            var countView = new CashCountView();
            if (!ShowActionDialog("Count cash", "Enter the physical cash count before closing this shift.",
                    "Close shift", countView, () => countView.TryReadCountedCash(out _)))
                return;

            try
            {
                await _api.CloseShiftAsync(_shift.ShiftId, countView.CountedCash);
                _shiftClosed = true;
                formBarcode.IsEnabled = false;
                txtNum.Text = "Shift closed";
                MessageBox.Show("The shift was closed by the POS API.", "Shift closed", MessageBoxButton.OK, MessageBoxImage.Information);
                await _api.SignOutAsync();
                _timer?.Stop();
                _connectivityTimer?.Stop();
                NavigationService?.Navigate(new LoginPage());
            }
            catch (Exception exception) when (exception is PosApiException or HttpRequestException)
            {
                MessageBox.Show($"Could not close the shift: {exception.Message}", "POS API", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private ShiftClosingSummary BuildShiftClosingSummary(decimal countedCash)
        {
            var closedAt = DateTime.Now;
            var sales = _salesHistory.ToList();
            var payments = sales.SelectMany(sale => sale.Payments).ToList();
            decimal SumForMethod(string marker) => payments
                .Where(payment => NormalizeForComparison(payment.Method).Contains(marker, StringComparison.Ordinal))
                .Sum(payment => payment.Amount);

            var paidTotal = sales.Sum(sale => sale.Payments.Count > 0
                ? sale.Payments.Sum(payment => payment.Amount)
                : sale.DiscountTotal);
            var salesCount = sales.Count;
            var bestSeller = sales.SelectMany(sale => sale.Items)
                .GroupBy(item => string.IsNullOrWhiteSpace(item.Sku) ? item.Name : item.Sku,
                    StringComparer.OrdinalIgnoreCase)
                .Select(group => new
                {
                    Product = group.First(),
                    Quantity = group.Sum(item => item.Quantity),
                    Revenue = group.Sum(item => item.LineTotal)
                })
                .OrderByDescending(item => item.Quantity)
                .ThenByDescending(item => item.Revenue)
                .FirstOrDefault();

            return new ShiftClosingSummary
            {
                EmployeeName = _currentEmployee?.Name ?? "Funcionário não identificado",
                StoreName = _storeName,
                StartedAt = _shiftStartedAt,
                ClosedAt = closedAt,
                Duration = closedAt - _shiftStartedAt,
                SalesCount = salesCount,
                ItemsSold = sales.Sum(sale => sale.Items.Count > 0
                    ? sale.Items.Sum(item => item.Quantity)
                    : sale.ItemCount),
                AverageTicket = salesCount > 0 ? paidTotal / salesCount : 0m,
                DebitTotal = SumForMethod("DEBITO"),
                CreditTotal = SumForMethod("CREDITO"),
                PixTotal = SumForMethod("PIX"),
                CashSalesTotal = SumForMethod("DINHEIRO"),
                RegularTotal = sales.Sum(sale => sale.RegularTotal),
                PaidTotal = paidTotal,
                PromotionSavings = sales.Sum(sale => sale.Earned),
                ExpectedCash = _cashBalance,
                CountedCash = countedCash,
                CashDifference = countedCash - _cashBalance,
                CashAdded = _cashAddedTotal,
                CashWithdrawn = _cashWithdrawnTotal,
                BestSellingProduct = bestSeller?.Product.Name ?? "Nenhum produto vendido",
                BestSellingQuantity = bestSeller?.Quantity ?? 0,
                BestSellingRevenue = bestSeller?.Revenue ?? 0m
            };
        }

        private static bool IsCashPayment(string method) =>
            NormalizeForComparison(method).Contains("DINHEIRO", StringComparison.Ordinal);

        private static string NormalizeForComparison(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var normalized = value.Normalize(NormalizationForm.FormD)
                .Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                .ToArray();
            return new string(normalized).ToUpperInvariant();
        }

        private void FindCustomer()
        {
            MessageBox.Show("Customer lookup is unavailable in this POS build.",
                "Customer lookup unavailable", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void RegisterCashMovement(bool isAddition)
        {
            var title = isAddition ? "Adicionar dinheiro" : "Retirar dinheiro";
            var culture = CultureInfo.GetCultureInfo("pt-BR");
            string amountText;
            string reason;
            bool confirmed;
            if (isAddition)
            {
                var view = new AddCashView(_cashBalance.ToString("C", culture));
                confirmed = ShowActionDialog(title, "Registre uma entrada de numerário no caixa.", "Registrar entrada", view);
                amountText = view.AmountText;
                reason = view.Reason;
            }
            else
            {
                var view = new WithdrawCashView(_cashBalance.ToString("C", culture));
                confirmed = ShowActionDialog(title, "Registre uma retirada de numerário do caixa.", "Registrar retirada", view);
                amountText = view.AmountText;
                reason = view.Reason;
            }

            if (!confirmed)
                return;

            if (!decimal.TryParse(amountText, NumberStyles.Currency, culture, out var amount) &&
                !decimal.TryParse(amountText, NumberStyles.Number, CultureInfo.InvariantCulture, out amount))
            {
                MessageBox.Show("Enter a valid amount.", title, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (amount <= 0 || (!isAddition && amount > _cashBalance) || string.IsNullOrWhiteSpace(reason) || _shift is null)
            {
                MessageBox.Show("Enter a positive amount and a reason. Withdrawals cannot exceed the recorded cash balance.", title,
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                await _api.CreateCashMovementAsync(_shift.ShiftId, amount, reason, withdrawal: !isAddition);
                _cashBalance += isAddition ? amount : -amount;
                if (isAddition) _cashAddedTotal += amount; else _cashWithdrawnTotal += amount;
                _cashMovements.Add($"{DateTime.Now:HH:mm} · {(isAddition ? "Entrada" : "Retirada")}: {amount.ToString("C", culture)} · {reason}");
            }
            catch (Exception exception) when (exception is PosApiException or HttpRequestException)
            {
                MessageBox.Show($"Could not save the cash movement: {exception.Message}", "POS API", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool ShowActionDialog(string title, string subtitle, string primaryText,
            UserControl content, Func<bool>? confirmAction = null, bool fullScreen = false)
        {
            var dialog = new ActionDialogWindow(title, subtitle, primaryText, content, fullScreen,
                content is SaleCustomerView ? 640 : 520)
            {
                Owner = Window.GetWindow(this),
                ConfirmAction = confirmAction
            };
            return dialog.ShowDialog() == true;
        }
    }

    }
