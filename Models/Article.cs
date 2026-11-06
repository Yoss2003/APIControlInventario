using InventoryAPI.Models.Interfaces;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace InventoryAPI.Models
{
    public class Article : INotifyPropertyChanged
    {
        private decimal _quantityInCart;

        // ==========================================
        // 1. IDENTIDAD PRINCIPAL
        // ==========================================
        [Key]
        public int Id { get; set; }

        [Required]
        public int InventoryId { get; set; }

        [Required]
        public int CategoryId { get; set; }

        [Required]
        public int? BrandId { get; set; }

        // ==========================================
        // 2. DATOS DE RASTREO
        // ==========================================
        [Required]
        [StringLength(100)]
        public string Code { get; set; } = string.Empty;

        [StringLength(150)]
        public string? Barcode { get; set; }

        [Required]
        [StringLength(250)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(150)]
        public string Model { get; set; } = string.Empty;

        [StringLength(150)]
        public string? SerialNumber { get; set; }

        [Required]
        public TrackingMode Tracking { get; set; }

        public string? Presentation { get; set; }

        // ==========================================
        // 3. LOGÍSTICA Y STOCK
        // ==========================================
        public int? LocationId { get; set; }

        public int? StatusId { get; set; }

        public int? ConditionId { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Stock { get; set; }

        [Required]
        [StringLength(50)]
        public string MeasurementUnit { get; set; } = "Unidades";

        [StringLength(50)]
        public string? AcquisitionUnit { get; set; }

        [StringLength(50)]
        public string? SaleUnit { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? ConversionFactor { get; set; }

        // ==========================================
        // 4. FINANZAS Y PROVEEDORES
        // ==========================================
        [Column(TypeName = "decimal(18,2)")]
        public decimal? AcquisitionPrice { get; set; }

        [StringLength(10)]
        public string? AcquisitionCurrency { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? SalePrice { get; set; }

        public string? SaleCurrency { get; set; }

        public int? SupplierId { get; set; }

        // ==========================================
        // 5. ASIGNACIONES (ACTIVO FIJO)
        // ==========================================
        public int? CurrentEmployeeId { get; set; }

        public int? PreviousEmployeeId { get; set; }

        [StringLength(100)]
        public string? FixedAsset { get; set; }

        public int? UsefulLifeMonths { get; set; }

        public DateTime? WarrantyEndDate { get; set; }

        // ==========================================
        // 6. FECHAS Y AUDITORÍA
        // ==========================================
        [Required]
        public DateTime RegistrationDate { get; set; } = DateTime.UtcNow;

        public DateTime? AcquisitionDate { get; set; }

        public DateTime? ModificationDate { get; set; }

        public DateTime? DepartureDate { get; set; }

        public DateTime? DecommissionDate { get; set; }

        [Required]
        public int ActionId { get; set; }

        // ==========================================
        // 7. MULTIMEDIA Y NOTAS
        // ==========================================
        public string? Characteristics { get; set; }

        public string? Observation { get; set; }

        public string? MainPhotoPath { get; set; }

        public string? MainVoucherPath { get; set; }

        // ==========================================
        // 8. PROPIEDADES VIRTUALES (No Mapeadas)
        // ==========================================
        [NotMapped]
        public decimal QuantityInCart
        {
            get => _quantityInCart;
            set
            {
                if (_quantityInCart != value)
                {
                    _quantityInCart = value;
                    OnPropertyChanged();
                }
            }
        }

        [NotMapped]
        public int? LoggedUserId { get; set; }

        [NotMapped]
        public string? LoggedUserFullName { get; set; }

        [Required]
        public bool IsActive { get; set; } = true;

        public DateTime? LastModified { get; set; }
        public int CompanyId { get; set; }

        public virtual ICollection<ArticleDetails> Details { get; set; } = [];

        // ==========================================
        // 9. MOTOR DE NOTIFICACIONES (INotifyPropertyChanged)
        // ==========================================
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}