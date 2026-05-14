using System.ComponentModel.DataAnnotations;

namespace InventoryAPI.Models
{
    public class SalesMode
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string SalesModeName { get; set; } = string.Empty;
    }
}