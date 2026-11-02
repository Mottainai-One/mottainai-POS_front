using System;
using System.Collections.Generic;

namespace PDVMottainai.Models
{
    public class CompletedSale
    {
        public int SaleNumber { get; init; }
        public DateTime SaleDate { get; init; } = DateTime.Now;
        public string CustomerName { get; init; } = "Consumidor não identificado";
        public string CustomerCpf { get; init; } = string.Empty;
        public bool IsMottainaiMember { get; init; }
        public bool CpfOnInvoice { get; init; }
        public int ItemCount { get; init; }
        public decimal RegularTotal { get; init; }
        public decimal DiscountTotal { get; init; }
        public decimal Earned { get; init; }
        public List<CompletedSaleItem> Items { get; init; } = new();
        public List<SalePaymentRecord> Payments { get; init; } = new();
    }

    public class CompletedSaleItem
    {
        public string Name { get; init; } = string.Empty;
        public string Sku { get; init; } = string.Empty;
        public int Quantity { get; init; }
        public decimal RegularUnitPrice { get; init; }
        public decimal AppliedUnitPrice { get; init; }
        public decimal LineTotal { get; init; }
    }

    public class SalePaymentRecord
    {
        public string Method { get; init; } = string.Empty;
        public decimal Amount { get; init; }
        public decimal CashReceived { get; init; }
        public decimal Change { get; init; }
        public string? Reference { get; init; }
    }

    public class PaymentSplit
    {
        public string Method { get; init; } = string.Empty;
        public decimal Amount { get; init; }
        public decimal CashReceived { get; init; }
        public decimal Change { get; init; }
        public string? Reference { get; init; }
        public System.Windows.Visibility ChangeVisibility =>
            Change > 0 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
    }
}
