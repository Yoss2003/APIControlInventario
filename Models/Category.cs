// Ruta en tu API: Models/Category.cs
using System.ComponentModel.DataAnnotations;

namespace InventoryAPI.Models
{
    public class Category
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int InventoryId { get; set; }

        // --- NUEVOS CAMPOS DE NUESTRA ARQUITECTURA ---
        public int? ParentCategoryId { get; set; } // Null = Padre, Número = Hija
        public string? TrackingMode { get; set; } // "Serializado" o "Estándar"
        public string? NamingMethod { get; set; } // Ej: "[Code] + [Model]"

        [Required]
        [StringLength(255)]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Required]
        public int IsReturnable { get; set; } = 1; // 1 = Sí, 0 = No

        public string? CreationDate { get; set; }
        public string? CreationUser { get; set; }
        public string? ModificationDate { get; set; }
        public string? ModificationUser { get; set; }
        public string? DeletionDate { get; set; }
        public string? DeletionUser { get; set; }
    }
}