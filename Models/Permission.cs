using System.ComponentModel.DataAnnotations;

namespace InventoryAPI.Models
{
    public class Permission
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty; // Ej: "Permitir Crear Artículos"

        [Required]
        [StringLength(50)]
        public string SystemCode { get; set; } = string.Empty; // Ej: "CREATE_ARTICLES" (Este es el que leeremos en el código)

        public List<RolePermission>? RolePermissions { get; set; }
    }
}