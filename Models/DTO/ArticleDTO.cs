using System.ComponentModel;

namespace InventoryAPI.Models.DTO
{
    public class ArticleDTO
    {
    }
    public enum ArticleType
    {
        Standard,
        Bulk,
        Serialized
    }

    // 2. El hijo único (El IMEI/Serie)
    public class ArticleSerialDto
    {
        public string SerialNumber { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
    }

    // 3. El Padre (La tarjeta del producto)
    public class InventoryArticleGroup : INotifyPropertyChanged
    {
        public int ArticleId { get; set; }
        public string ArticleName { get; set; } = string.Empty;
        public string Sku { get; set; } = string.Empty;
        public decimal TotalStock { get; set; }
        public ArticleType Type { get; set; }

        public List<ArticleSerialDto> Serials { get; set; } = [];

        public bool IsSerialized => Type == ArticleType.Serialized;

        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                _isExpanded = value;
                OnPropertyChanged(nameof(IsExpanded));
                OnPropertyChanged(nameof(ExpansionIcon));
            }
        }

        public string ExpansionIcon => IsExpanded ? "🔼" : "🔽";

        public void ToggleExpansion()
        {
            if (IsSerialized) IsExpanded = !IsExpanded;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
