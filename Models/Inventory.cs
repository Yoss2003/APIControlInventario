using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;

namespace InventoryAPI.Models
{
    public class Inventory
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(255)]
        public string InventoryName { get; set; } = string.Empty;

        [Required]
        public DateTime CreationDate { get; set; }

        public DateTime ModificationDate { get; set; }

        public int UserId { get; set; }

        public string Username { get; set; } = string.Empty;

        public string? Alias { get; set; }
        public bool IsActive { get; set; } = true;
        public int CompanyId { get; set; }
    }
}