using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using PDVMottainai.Services;

namespace PDVMottainai.Views;

public partial class LoginPage : Page
{
    private readonly PosApiClient _api = new();
    private PosAvailableAccess? _availableAccess;

    public ObservableCollection<PosStoreAccess> Stores { get; } = new();
    public ObservableCollection<PosTerminalAccess> Terminals { get; } = new();

    public LoginPage()
    {
        InitializeComponent();
        store.ItemsSource = Stores;
        terminal.ItemsSource = Terminals;
    }

    private void loja_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        terminal.IsEnabled = Terminals.Count > 0;
        if (terminal.SelectedItem is null && Terminals.Count > 0)
            terminal.SelectedIndex = 0;
    }

    private void TogglePassword_Click(object sender, RoutedEventArgs e)
    {
        if (txtPasswordClose.Visibility == Visibility.Visible)
        {
            imgEye.Source = new BitmapImage(new Uri("pack://application:,,,/IMG/eyeOpenIcon.png"));
            txtPasswordOpen.Text = txtPasswordClose.Password;
            txtPasswordClose.Visibility = Visibility.Collapsed;
            txtPasswordOpen.Visibility = Visibility.Visible;
        }
        else
        {
            imgEye.Source = new BitmapImage(new Uri("pack://application:,,,/IMG/eyeCloseIcon.png"));
            txtPasswordClose.Password = txtPasswordOpen.Text;
            txtPasswordOpen.Visibility = Visibility.Collapsed;
            txtPasswordClose.Visibility = Visibility.Visible;
        }
    }

    private void HelpButton_Click(object sender, RoutedEventArgs e)
    {
        const string phoneNumber = "5511956775122";
        const string message = "Olá, preciso de ajuda com o PDV Mottainai.";
        var url = $"https://wa.me/{phoneNumber}?text={Uri.EscapeDataString(message)}";
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception)
        {
            ShowError("Não foi possível abrir o WhatsApp. Verifique se ele está disponível neste dispositivo.");
        }
    }

    private void CredentialsChanged(object sender, RoutedEventArgs e)
    {
        if (_availableAccess is null)
            return;
        _availableAccess = null;
        Stores.Clear();
        Terminals.Clear();
        LoginErrorPanel.Visibility = Visibility.Collapsed;
    }

    private void CredentialsChanged(object sender, TextChangedEventArgs e) => CredentialsChanged(sender, new RoutedEventArgs());

    private async void LogIn_Click(object sender, RoutedEventArgs e)
    {
        var password = txtPasswordClose.Visibility == Visibility.Visible ? txtPasswordClose.Password : txtPasswordOpen.Text;
        if (string.IsNullOrWhiteSpace(txtEmail.Text) || string.IsNullOrWhiteSpace(password))
        {
            ShowError("Informe seu e-mail e sua senha.");
            return;
        }

        try
        {
            if (_availableAccess is null)
            {
                SetBusy(true);
                _availableAccess = await _api.AuthenticateFirebaseAsync(txtEmail.Text.Trim(), password);
                Stores.Clear();
                foreach (var item in _availableAccess.Stores)
                    Stores.Add(item);
                Terminals.Clear();
                foreach (var item in _availableAccess.Terminals)
                    Terminals.Add(item);
                if (Stores.Count == 1)
                    store.SelectedIndex = 0;
                if (Terminals.Count == 1)
                    terminal.SelectedIndex = 0;

                if (Terminals.Count == 0)
                    throw new PosApiException("No active POS terminal is assigned to this operator.", System.Net.HttpStatusCode.Forbidden);
                if (Terminals.Count > 1)
                {
                    ShowError("Access confirmed. Select the store and terminal, then choose Start shift again.");
                    return;
                }
            }

            if (terminal.SelectedItem is not PosTerminalAccess selectedTerminal)
            {
                ShowError("Select an available POS terminal to continue.");
                return;
            }

            SetBusy(true);
            var session = await _api.SelectTerminalAsync(selectedTerminal.TerminalCode);
            NavigationService?.Navigate(new salePage(_api, session));
        }
        catch (PosApiException exception)
        {
            ShowError($"POS API: {exception.Message}");
        }
        catch (HttpRequestException exception)
        {
            ShowError($"Could not reach the POS API: {exception.Message}");
        }
        catch (TaskCanceledException)
        {
            ShowError("The request timed out. Check the POS API and network connection.");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool isBusy)
    {
        Mouse.OverrideCursor = isBusy ? System.Windows.Input.Cursors.Wait : null;
        txtEmail.IsEnabled = !isBusy;
        txtPasswordClose.IsEnabled = !isBusy;
        txtPasswordOpen.IsEnabled = !isBusy;
        store.IsEnabled = !isBusy && Stores.Count > 0;
        terminal.IsEnabled = !isBusy && Terminals.Count > 0;
    }

    private void ShowError(string message)
    {
        txtMsg.Text = message;
        LoginErrorPanel.Visibility = Visibility.Visible;
    }
}
