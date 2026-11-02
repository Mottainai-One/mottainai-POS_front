using System;

namespace PDVMottainai.Models
{
    public sealed class ShiftClosingSummary
    {
        public string EmployeeName { get; init; } = "Funcionário não identificado";
        public string StoreName { get; init; } = "Loja não informada";
        public DateTime StartedAt { get; init; }
        public DateTime ClosedAt { get; init; }
        public TimeSpan Duration { get; init; }
        public int SalesCount { get; init; }
        public int ItemsSold { get; init; }
        public decimal AverageTicket { get; init; }
        public decimal DebitTotal { get; init; }
        public decimal CreditTotal { get; init; }
        public decimal PixTotal { get; init; }
        public decimal CashSalesTotal { get; init; }
        public decimal RegularTotal { get; init; }
        public decimal PaidTotal { get; init; }
        public decimal PromotionSavings { get; init; }
        public decimal ExpectedCash { get; init; }
        public decimal CountedCash { get; init; }
        public decimal CashDifference { get; init; }
        public decimal CashAdded { get; init; }
        public decimal CashWithdrawn { get; init; }
        public string BestSellingProduct { get; init; } = "Nenhum produto vendido";
        public int BestSellingQuantity { get; init; }
        public decimal BestSellingRevenue { get; init; }
    }
}