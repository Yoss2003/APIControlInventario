using InventoryAPI.Models.Interfaces;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventoryAPI.Models
{
    public class Brand
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int InventoryId { get; set; }

        [Required]
        public int CategoryId { get; set; }

        [Required]
        [StringLength(255)]
        public string Name { get; set; } = string.Empty;
        public string NameKey { get; set; } = string.Empty;
        [NotMapped]
        public bool Reused { get; set; }
        public bool IsActive { get; set; } = true;
        public int CompanyId { get; set; }
    }
}