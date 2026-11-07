using InventoryAPI.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventoryAPI.Models
{ 
    public class Category
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int InventoryId { get; set; }

        public int? ParentCategoryId { get; set; }

        public string? TrackingMode { get; set; }

        public string? NamingMethod { get; set; }

        [Required]
        [StringLength(255)]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }
        public string NameKey { get; set; } = string.Empty;
        [NotMapped]
        public bool Reused { get; set; }

        [Required]
        public bool IsReturnable { get; set; } = true;

        public DateTime? CreationDate { get; set; }
        public string? CreationUser { get; set; }
        public DateTime? ModificationDate { get; set; }
        public string? ModificationUser { get; set; }
        public DateTime? DeletionDate { get; set; }
        public string? DeletionUser { get; set; }
        public bool IsActive { get; set; } = true;
        public int CompanyId { get; set; }

        public string? Label1 { get; set; }
        public bool IsUnique1 { get; set; }
        public string? Label2 { get; set; }
        public bool IsUnique2 { get; set; }
        public string? Label3 { get; set; }
        public bool IsUnique3 { get; set; }
        public string? Label4 { get; set; }
        public bool IsUnique4 { get; set; }
        public string? Label5 { get; set; }
        public bool IsUnique5 { get; set; }
        public string? Label6 { get; set; }
        public bool IsUnique6 { get; set; }

        [NotMapped]
        public List<int>? SelectedUnitIds { get; set; } = [];

        public List<CategoryMeasurementUnit>? CategoryMeasurementUnits { get; set; }
    }
}