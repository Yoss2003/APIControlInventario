using System;
using System.ComponentModel.DataAnnotations;

namespace InventoryAPI.Models
{
    // Opcional: Si en tu API no tienes el enum TrackingMode, puedes crearlo aquí mismo 
    // o simplemente usar 'int' para guardar el estado (0 = Serializado, 1 = Estándar)
    public enum TrackingMode { Serializado = 0, Standard = 1, Bulk = 2 }

    public class Article
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public int InventoryId { get; set; }

        // --- DATOS PRINCIPALES DEL CATÁLOGO ---
        [Required] public string Code { get; set; } = string.Empty;
        public string? Barcode { get; set; }
        [Required] public string Name { get; set; } = string.Empty;
        [Required] public string Model { get; set; } = string.Empty;
        public int CategoryId { get; set; }
        public int BrandId { get; set; }

        // --- EL CORAZÓN DE LA FLEXIBILIDAD ---
        public TrackingMode Tracking { get; set; } = TrackingMode.Standard;
        public string MeasurementUnit { get; set; } = "Unidades";
        public decimal Stock { get; set; } = 0;

        // --- DATOS ESPECÍFICOS PARA EMPRESAS (ACTIVOS FIJOS) ---
        public string? SerialNumber { get; set; }
        public int? CurrentEmployeeId { get; set; }
        public int? PreviousEmployeeId { get; set; }
        public string? FixedAsset { get; set; }

        // --- PRECIOS Y COSTOS ---
        public decimal? AcquisitionPrice { get; set; }
        public decimal? SalePrice { get; set; }
        public string? AcquisitionCurrency { get; set; }

        // --- FECHAS (Actualizadas a DateTime) ---
        public DateTime? AcquisitionDate { get; set; }
        public DateTime? DecommissionDate { get; set; }
        public DateTime? WarrantyEndDate { get; set; }

        // --- ESTADOS Y UBICACIÓN ---
        public int StatusId { get; set; }
        public int LocationId { get; set; }
        public int ConditionId { get; set; }

        // --- ARCHIVOS Y OBSERVACIONES ---
        public string? Observation { get; set; }
        public string? MainPhotoPath { get; set; }
        public string? SecondaryPhotoPath { get; set; }
        public string? MainVoucherPath { get; set; }
        public string? SecondaryVoucherPath { get; set; }

        // --- OTROS DATOS TÉCNICOS Y AUDITORÍA ---
        public string? SupplierRuc { get; set; }
        public string? SupplierName { get; set; }
        public int? UsefulLifeMonths { get; set; }
        public string? Characteristics { get; set; }

        public DateTime? RegistrationDate { get; set; }
        public DateTime? ModificationDate { get; set; }
        public DateTime? DepartureDate { get; set; }
        public int ActionId { get; set; }
        public int? RegistrationGroupId { get; set; }
    }
}