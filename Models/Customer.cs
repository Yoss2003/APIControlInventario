using InventoryAPI.Models.Interfaces;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventoryAPI.Models
{
    public class Customer
    {
        [Key]
        public int Id { get; set; }

        [StringLength(50)]
        public string? DocumentNumber { get; set; }

        [Required]
        [StringLength(255)]
        public string Name { get; set; } = string.Empty;

        [StringLength(50)]
        public string? Phone { get; set; }

        [StringLength(255)]
        public string? Email { get; set; }

        public string? Address { get; set; }

        [StringLength(255)]
        public string? RegistrationDate { get; set; }
        public bool IsActive { get; set; } = true;
        public int CompanyId { get; set; }

        [NotMapped]
        public string Initial => string.IsNullOrEmpty(Name) ? "" : Name[0].ToString().ToUpper();
    }
}