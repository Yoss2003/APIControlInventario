using System.ComponentModel.DataAnnotations;

namespace InventoryAPI.Models
{
    public class RolePermission
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int RoleId { get; set; }
        public virtual Role? Role { get; set; }
        
        [Required]
        public int PermissionId { get; set; }
        public virtual Permission? Permission { get; set; }
    }
}