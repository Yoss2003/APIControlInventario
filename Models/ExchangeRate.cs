using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventoryAPI.Models
{
    public class ExchangeRate
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(10)]
        public string BaseCurrency { get; set; } = "USD"; // Siempre "USD" para SUNAT

        [Required]
        [StringLength(10)]
        public string QuoteCurrency { get; set; } = "PEN"; // Siempre "PEN" para Perú (Soles)

        [Required]
        [Column(TypeName = "decimal(18,4)")]
        public decimal BuyPrice { get; set; } // Tipo de cambio: Compra (Ej: 3.7450)

        [Required]
        [Column(TypeName = "decimal(18,4)")]
        public decimal SellPrice { get; set; } // Tipo de cambio: Venta (Ej: 3.7550)

        [Required]
        public DateTime Date { get; set; } // Fecha oficial del tipo de cambio asignado por SUNAT
    }
}