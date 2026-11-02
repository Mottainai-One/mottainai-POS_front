using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
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
        private string _currentCustomerName = "Consumidor não identificado";
        private string _currentCustomerCpf = string.Empty;
        private int _saleNumber = 1;

        private enum SaleCustomerPromptResult
        {
            Cancelled,
            Continued
        }

        public ObservableCollection<ScannedProductLine> ScannedProducts { get; } = new();

        public salePage()
        {
            
            InitializeComponent();
            SetSaleStatus("Venda em andamento");
            DataContext = this;
            scannedProductsList.ItemsSource = ScannedProducts;
            UpdateSaleTotals();
            ChangeDateTime();
           
            
        }
        public salePage(string store, string emailUser)
        {
            InitializeComponent();
            SetSaleStatus("Venda em andamento");
            DataContext = this;
            scannedProductsList.ItemsSource = ScannedProducts;
            UpdateSaleTotals();

            int numSale = 0;
            TrendingSaleInformations(numSale, store, emailUser);
        }

        public void TrendingSaleInformations(int numSale, string storeSelected, string emailUser)
        {
            // Número da venda do turno
            numSale += 1;
            txtNumSale.Text = numSale.ToString();

            // Data e hora
            ChangeDateTime();

            // Loja
            _storeName = storeSelected;
            txtStore.Text = "Loja: " + storeSelected;

            var loggedInUser = MockApiService.Users.FirstOrDefault(user =>
                string.Equals(user.Email, emailUser, StringComparison.OrdinalIgnoreCase));
            _currentEmployee = loggedInUser is null
                ? MockApiService.Employees.FirstOrDefault(employee =>
                    string.Equals(employee.Email, emailUser, StringComparison.OrdinalIgnoreCase))
                : MockApiService.Employees.FirstOrDefault(employee => employee.EmployeeId == loggedInUser.EmployeeId);

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

        private void AddProductFromBarcode()
        {
            var barcode = formBarcode.Text.Trim();
            if (string.IsNullOrWhiteSpace(barcode))
                return;

            var productFound = MockApiService.Products.FirstOrDefault(
                product => product.Active &&
                           string.Equals(product.Barcode?.Trim(), barcode, StringComparison.OrdinalIgnoreCase));

            if (productFound == null)
            {
                txtProductName.Text = "-";
                txtBarCode.Text = "-";
                txtSkuProduct.Text = "-";
                MessageBox.Show("Produto não encontrado. Confira o código de barras.", "Produto não encontrado",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                formBarcode.SelectAll();
                formBarcode.Focus();
                return;
            }

            if (ScannedProducts.Count == 0 && !_customerDecisionMade)
            {
                var customerPrompt = RequestSaleCustomer("Antes de registrar o primeiro item, informe o CPF do cliente ou escolha continuar sem CPF.");
                if (customerPrompt == SaleCustomerPromptResult.Cancelled)
                {
                    formBarcode.SelectAll();
                    formBarcode.Focus();
                    return;
                }
                SetSaleStatus("Venda em andamento");
                txtNumSale.Text = _saleNumber.ToString();
            }

            txtProductName.Text = productFound.Name;
            txtBarCode.Text = productFound.Barcode;
            txtSkuProduct.Text = productFound.Sku;

            var existingLine = ScannedProducts.FirstOrDefault(
                line => line.Product.ProductId == productFound.ProductId);

            if (existingLine == null)
                ScannedProducts.Add(new ScannedProductLine(productFound, _customerIsMottainai));
            else
                existingLine.Quantity++;

            UpdateSaleTotals();
            formBarcode.Clear();
            formBarcode.Focus();
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
                        "Concluir", new ConsultProductView(), fullScreen: true);
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

        private void StartSale()
        {
            if (_shiftClosed)
            {
                ShowActionDialog("Turno encerrado", "Não é possível iniciar outra venda neste turno.",
                    "Fechar", new FinalizeShiftView(_salesHistory.Count, 0,
                        _cashBalance.ToString("C", CultureInfo.GetCultureInfo("pt-BR"))));
                return;
            }

            var startView = new StartSaleView(ScannedProducts.Count > 0);
            if (!ShowActionDialog("Iniciar venda", "Abra uma nova venda no terminal.",
                    "Continuar", startView))
                return;

            if (RequestSaleCustomer("Informe o CPF para verificar as promoções ou escolha continuar sem CPF.")
                == SaleCustomerPromptResult.Cancelled)
                return;

            ScannedProducts.Clear();
            txtProductName.Text = "-";
            txtBarCode.Text = "-";
            txtSkuProduct.Text = "-";
            SetSaleStatus("Venda em andamento");
            txtNumSale.Text = _saleNumber.ToString();
            UpdateCustomerCard();
            formBarcode.Clear();
            UpdateSaleTotals();

            // ShowDialog desativa a janela principal. Só devolva o foco ao leitor
            // depois que o popup fechar; focar dentro do callback deixa o PDV sem cursor.
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, () =>
            {
                formBarcode.Focus();
                Keyboard.Focus(formBarcode);
            });
        }

        private void CancelSale()
        {
            var canCancel = ScannedProducts.Count > 0;
            var view = new CancelSaleView(canCancel);
            ShowActionDialog("Cancelar venda",
                canCancel ? "Revise o impacto antes de continuar." : "Não há itens na venda atual.",
                canCancel ? "Cancelar venda" : "Entendi", view, () =>
                {
                    if (canCancel)
                    {
                        ScannedProducts.Clear();
                        SetSaleStatus("Venda cancelada");
                        _customerIsMottainai = false;
                        _customerWantsCpfInvoice = false;
                        _customerDecisionMade = false;
                        _currentCustomerName = "Consumidor não identificado";
                        _currentCustomerCpf = string.Empty;
                        UpdateCustomerCard();
                        UpdateSaleTotals();
                    }
                    formBarcode.Focus();
                    return true;
                });
        }

        private void FinalizeSale()
        {
            var totals = CalculateSaleTotals();
            var view = new FinalizeSaleView(ScannedProducts, totals.Discount, _currentCustomerName,
                _currentCustomerCpf, _customerWantsCpfInvoice, _saleNumber);
            var hasItems = ScannedProducts.Count > 0;
            ShowActionDialog("Finalizar venda",
                hasItems ? "Confira os itens e divida o pagamento entre um ou mais meios." : "Adicione produtos antes de finalizar.",
                hasItems ? "Finalizar venda" : "Voltar à venda", view, hasItems ? () =>
                {
                    if (!view.IsPaymentComplete)
                    {
                        view.ShowFeedback($"Adicione pagamentos para completar {view.Remaining.ToString("C", CultureInfo.GetCultureInfo("pt-BR"))}.");
                        return false;
                    }

                    var itemCount = ScannedProducts.Sum(line => line.Quantity);
                    var paymentRecords = view.GetPaymentRecords().ToList();
                    _salesHistory.Add(new CompletedSale
                    {
                        SaleNumber = _saleNumber++,
                        SaleDate = DateTime.Now,
                        CustomerName = _currentCustomerName,
                        CustomerCpf = _currentCustomerCpf,
                        IsMottainaiMember = _customerIsMottainai,
                        CpfOnInvoice = _customerWantsCpfInvoice && !string.IsNullOrEmpty(_currentCustomerCpf),
                        ItemCount = itemCount,
                        RegularTotal = totals.Regular,
                        DiscountTotal = totals.Discount,
                        Earned = totals.Earned,
                        Items = ScannedProducts.Select(line => new CompletedSaleItem
                        {
                            Name = line.Product.Name,
                            Sku = line.Product.Sku,
                            Quantity = line.Quantity,
                            RegularUnitPrice = line.RegularUnitPrice,
                            AppliedUnitPrice = line.AppliedUnitPrice,
                            LineTotal = line.LineTotal
                        }).ToList(),
                        Payments = paymentRecords
                    });
                    _cashBalance += paymentRecords.Where(payment => IsCashPayment(payment.Method)).Sum(payment => payment.Amount);
                    ScannedProducts.Clear();
                    SetSaleStatus("Venda finalizada");
                    _customerIsMottainai = false;
                    _customerWantsCpfInvoice = false;
                    _customerDecisionMade = false;
                    _currentCustomerName = "Consumidor não identificado";
                    _currentCustomerCpf = string.Empty;
                    UpdateCustomerCard();
                    UpdateSaleTotals();
                    formBarcode.Focus();
                    return true;
                } : null, fullScreen: true);
        }

        private void RemoveSelectedProduct()
        {
            var view = new RemoveProductView(ScannedProducts);
            var hasItems = ScannedProducts.Count > 0;
            ShowActionDialog("Excluir produto", "Selecione uma linha para remover uma unidade do item.",
                hasItems ? "Excluir unidade" : "Entendi", view, () =>
                {
                    if (!hasItems)
                        return true;
                    if (view.SelectedLine is not { } selectedLine)
                    {
                        view.ShowFeedback("Selecione um item da lista para continuar.");
                        return false;
                    }

                    if (selectedLine.Quantity > 1)
                        selectedLine.Quantity--;
                    else
                        ScannedProducts.Remove(selectedLine);
                    UpdateSaleTotals();
                    return true;
                });
        }

        private void ShowSalesHistory()
        {
            ShowActionDialog("Histórico de vendas", "Selecione uma venda para consultar todos os detalhes.",
                "Fechar", new SalesHistoryView(_salesHistory), fullScreen: true);
        }

        private void ShowPromotions()
        {
            var promotions = MockApiService.Products
                .Where(product => product.Active && product.RegularPrice.HasValue &&
                                  product.DiscountPrice.HasValue &&
                                  product.DiscountPrice.Value < product.RegularPrice.Value)
                .ToList();
            ShowActionDialog("Promoções", "Consulte todas as ofertas ativas no catálogo.",
                "Fechar", new PromotionsView(promotions), fullScreen: true);
        }

        private void FinalizeShift()
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

            var countView = new CashCountView();
            var countConfirmed = ShowActionDialog("Contagem do caixa",
                "Informe o valor contado fisicamente. O saldo esperado ficará oculto até a conferência.",
                "Conferir caixa", countView, () =>
                {
                    if (!countView.TryReadCountedCash(out _))
                        return false;
                    return true;
                });
            if (!countConfirmed)
                return;

            var summary = BuildShiftClosingSummary(countView.CountedCash);
            var summaryView = new FinalizeShiftView(summary);
            var turnFinished = ShowActionDialog("Resumo do turno",
                "Confira as vendas, formas de pagamento e a conferência do caixa antes de encerrar.",
                "Finalizar turno", summaryView, () =>
                {
                    _shiftClosed = true;
                    formBarcode.IsEnabled = false;
                    txtNum.Text = "Turno finalizado";
                    return true;
                }, fullScreen: true);

            if (turnFinished)
            {
                _timer?.Stop();
                _connectivityTimer?.Stop();
                NavigationService?.Navigate(new LoginPage());
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
            var view = new FindCustomerView();
            ShowActionDialog("Buscar cliente", "Pesquise pelo CPF para vincular um cadastro à venda.",
                "Vincular cliente", view, () =>
                {
                    if (!view.CustomerFound)
                    {
                        view.ShowFeedback("Busque um CPF válido com 11 números antes de continuar.");
                        return false;
                    }

                    _currentCustomerName = view.CustomerName;
                    _currentCustomerCpf = view.CustomerCpf;
                    _customerIsMottainai = view.IsMottainaiMember;
                    _customerDecisionMade = true;
                    UpdateCustomerCard();
                    UpdateSaleTotals();
                    return true;
                });
        }

        private void RegisterCashMovement(bool isAddition)
        {
            var title = isAddition ? "Adicionar dinheiro" : "Retirar dinheiro";
            var culture = CultureInfo.GetCultureInfo("pt-BR");
            if (isAddition)
            {
                var view = new AddCashView(_cashBalance.ToString("C", culture));
                ShowActionDialog(title, "Registre uma entrada de numerário no caixa.",
                    "Registrar entrada", view, () => ApplyCashMovement(view.AmountText, view.Reason, true, view.ShowFeedback, culture));
            }
            else
            {
                var view = new WithdrawCashView(_cashBalance.ToString("C", culture));
                ShowActionDialog(title, "Registre uma retirada de numerário do caixa.",
                    "Registrar retirada", view, () => ApplyCashMovement(view.AmountText, view.Reason, false, view.ShowFeedback, culture));
            }
        }

        private bool ApplyCashMovement(string amountText, string reason, bool isAddition,
            Action<string> showFeedback, CultureInfo culture)
        {
            if (!decimal.TryParse(amountText, NumberStyles.Currency, culture, out var amount) &&
                !decimal.TryParse(amountText, NumberStyles.Number, CultureInfo.InvariantCulture, out amount))
            {
                showFeedback("Informe um valor válido, como 25,00.");
                return false;
            }
            if (amount <= 0)
            {
                showFeedback("O valor precisa ser maior que zero.");
                return false;
            }
            if (!isAddition && amount > _cashBalance)
            {
                showFeedback($"Saldo insuficiente. Saldo registrado: {_cashBalance.ToString("C", culture)}.");
                return false;
            }

            _cashBalance += isAddition ? amount : -amount;
            if (isAddition)
                _cashAddedTotal += amount;
            else
                _cashWithdrawnTotal += amount;
            var movementName = isAddition ? "Entrada" : "Retirada";
            var reasonSuffix = string.IsNullOrWhiteSpace(reason) ? string.Empty : $" · {reason}";
            _cashMovements.Add($"{DateTime.Now:HH:mm} · {movementName}: {amount.ToString("C", culture)}{reasonSuffix}");
            showFeedback($"Movimentação registrada. Novo saldo: {_cashBalance.ToString("C", culture)}.");
            return true;
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
