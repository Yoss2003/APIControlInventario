using System.ComponentModel.DataAnnotations;

namespace InventoryAPI.Models
{
    public class Currency
    {
        [Key]
        public int Id { get; set; }
        public string? CurrencyCode { get; set; }

        [Required]
        [StringLength(100)]
        public string CurrencyName { get; set; } = string.Empty;
    }
}