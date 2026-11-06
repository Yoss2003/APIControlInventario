using System.ComponentModel.DataAnnotations;

namespace InventoryAPI.Models
{
    public class TimeZoneItem
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string TimeZoneName { get; set; } = string.Empty;
    }
}