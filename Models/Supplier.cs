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
        // Descripción interna y opcional: por qué trabajamos con este proveedor.
        [StringLength(700)]
        public string? RelationshipDescription { get; set; }

        // Instantánea de campos públicos de la consulta SUNAT avanzada.
        // No contiene tokens; los datos fiscales son informativos e históricos.
        [StringLength(10000)]
        public string? FiscalDataJson { get; set; }

        // Detalle opcional de la consulta RUC avanzada. No crea columnas
        // ni sustituye los datos de contacto registrados manualmente.
        [NotMapped] public bool AdvancedDataAvailable { get; set; }
        [NotMapped] public bool LookupFromCache { get; set; }
        [NotMapped] public string? Ubigeo { get; set; }
        [NotMapped] public string? TipoContribuyente { get; set; }
        [NotMapped] public string? ActividadEconomica { get; set; }
        [NotMapped] public string? NumeroTrabajadores { get; set; }
        [NotMapped] public string? TipoFacturacion { get; set; }
        [NotMapped] public string? TipoContabilidad { get; set; }
        [NotMapped] public string? ComercioExterior { get; set; }
        [NotMapped]
        public bool Reused { get; set; }
        public bool IsActive { get; set; } = true;
        public int CompanyId { get; set; }
    }
}