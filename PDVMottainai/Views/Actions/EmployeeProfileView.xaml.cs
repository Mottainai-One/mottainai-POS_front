using PDVMottainai.Models;
using System.Windows.Controls;
using System.Windows.Threading;

namespace PDVMottainai.Views.Actions
{
    public partial class EmployeeProfileView : UserControl
    {
        private readonly DateTime _shiftStartedAt;
        private readonly DispatcherTimer _durationTimer;

        internal EmployeeProfileView(Employee? employee, string storeName, int completedSales, DateTime shiftStartedAt)
        {
            InitializeComponent();
            _shiftStartedAt = shiftStartedAt;

            EmployeeName.Text = employee?.Name ?? "Funcionário não identificado";
            EmployeePosition.Text = employee is null ? "Perfil do turno" : $"Perfil do turno · Cargo {employee.RoleId}";
            SalesCount.Text = completedSales.ToString();
            ShiftStart.Text = shiftStartedAt.ToString("dd/MM/yyyy · HH:mm:ss");
            EmployeeCpf.Text = FormatCpf(employee?.Cpf);
            EmployeeEmail.Text = Display(employee?.Email);
            EmployeePhone.Text = Display(employee?.Phone);
            StoreName.Text = Display(storeName);
            EmployeeId.Text = employee?.EmployeeId.ToString() ?? "Não disponível";
            RoleId.Text = employee?.RoleId.ToString() ?? "Não disponível";
            HireDate.Text = employee?.HireDate.ToString("dd/MM/yyyy") ?? "Não disponível";
            EmployeeStatus.Text = employee is null ? "Não identificado" : employee.Active ? "Ativo" : "Inativo";
            if (employee is null)
                DataNotice.Text = "Não foi possível localizar o cadastro do funcionário pelo e-mail do login. Os dados pessoais não estão disponíveis.";

            _durationTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _durationTimer.Tick += (_, _) => UpdateShiftDuration();
            _durationTimer.Start();
            Unloaded += (_, _) => _durationTimer.Stop();
            UpdateShiftDuration();
        }

        private void UpdateShiftDuration()
        {
            var elapsed = DateTime.Now - _shiftStartedAt;
            var days = (int)elapsed.TotalDays;
            ShiftDuration.Text = days > 0
                ? $"{days}d {elapsed.Hours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}"
                : $"{elapsed.Hours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}";
        }

        private static string Display(string? value) => string.IsNullOrWhiteSpace(value) ? "Não informado" : value;

        private static string FormatCpf(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "Não informado";
            var digits = new string(value.Where(char.IsDigit).ToArray());
            return digits.Length == 11 ? $"{digits[..3]}.{digits[3..6]}.{digits[6..9]}-{digits[9..]}" : value;
        }
    }
}
