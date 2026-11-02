using System.ComponentModel;

namespace PDVMottainai.Models
{
    public sealed class ScannedProductLine : INotifyPropertyChanged
    {
        private bool _isMottainaiMember;
        private int _quantity = 1;

        public Product Product { get; }
        public decimal RegularUnitPrice => Product.RegularPrice ?? 0m;
        public decimal AppliedUnitPrice => _isMottainaiMember
            ? Product.DiscountPrice ?? RegularUnitPrice
            : RegularUnitPrice;
        public decimal? EligibleDiscountPrice => _isMottainaiMember ? Product.DiscountPrice : null;
        public decimal LineTotal => AppliedUnitPrice * Quantity;

        public bool IsMottainaiMember
        {
            get => _isMottainaiMember;
            set
            {
                if (_isMottainaiMember == value) return;
                _isMottainaiMember = value;
                OnPropertyChanged(nameof(AppliedUnitPrice));
                OnPropertyChanged(nameof(EligibleDiscountPrice));
                OnPropertyChanged(nameof(LineTotal));
            }
        }

        public int Quantity
        {
            get => _quantity;
            set
            {
                if (_quantity == value) return;
                _quantity = value;
                OnPropertyChanged(nameof(Quantity));
                OnPropertyChanged(nameof(LineTotal));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public ScannedProductLine(Product product, bool isMottainaiMember = false)
        {
            Product = product;
            _isMottainaiMember = isMottainaiMember;
        }

        private void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}