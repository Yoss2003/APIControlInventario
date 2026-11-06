using System.ComponentModel.DataAnnotations;

namespace InventoryAPI.Models
{
    public class MeasurementUnit
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string UnitName { get; set; } = string.Empty;

        [Required]
        [StringLength(10)]
        public string Abbreviation { get; set; } = string.Empty;
    }
}