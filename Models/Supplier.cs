using InventoryAPI.Models.Interfaces;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventoryAPI.Models
{
    public class Supplier
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int InventoryId { get; set; } = 0;

        [Required]
        [StringLength(11)]
        public string Ruc { get; set; } = string.Empty;

        [Required]
        [StringLength(250)]
        public string BusinessName { get; set; } = string.Empty;

        [StringLength(150)]
        public string? ContactName { get; set; }

        [StringLength(50)]
        public string? Phone { get; set; }

        [StringLength(100)]
        public string? Email { get; set; }

        [StringLength(500)]
        public string? Address { get; set; }

        [Required]
        public int StatusId { get; set; } = 1;

        // 🌟 NUEVOS CAMPOS ADAPTADOS DEL ENDPOINT AVANZADO
        [StringLength(50)]
        public string? Estado { get; set; }       // Ej: ACTIVO

        [StringLength(50)]
        public string? Condicion { get; set; }    // Ej: HABIDO

        [StringLength(100)]
        public string? Distrito { get; set; }

        [StringLength(100)]
        public string? Provincia { get; set; }

        [StringLength(100)]
        public string? Departamento { get; set; }
        [NotMapped]
        public bool Reused { get; set; }
        public bool IsActive { get; set; } = true;
        public int CompanyId { get; set; }
    }
}