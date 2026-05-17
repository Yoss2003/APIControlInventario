using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventoryAPI.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required] public string FirstName { get; set; } = string.Empty;
        [Required] public string LastName { get; set; } = string.Empty;
        [Required] public string Email { get; set; } = string.Empty;
        [Required] public string Username { get; set; } = string.Empty;
        [Required] public string Password { get; set; } = string.Empty; // Solo vive aquí

        public int Age { get; set; }
        public string BirthDate { get; set; } = string.Empty;
        public string HireDate { get; set; } = string.Empty;

        // --- NUEVOS CAMPOS PREMIUM ---
        public string PhoneNumber { get; set; } = string.Empty;
        public string ProfilePictureUrl { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;

        // --- RELACIONES HACIA LAS TABLAS (Foreign Keys) ---
        public int RoleId { get; set; }
        public virtual Role? Role { get; set; }
        public int JobPositionId { get; set; }
        public int AreaId { get; set; }
        public int ContractTypeId { get; set; }
    }
}