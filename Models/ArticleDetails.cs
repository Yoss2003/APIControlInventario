using InventoryAPI.Models.Interfaces;
using System.ComponentModel.DataAnnotations;

namespace InventoryAPI.Models
{
    public class ArticleDetails
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ArticleId { get; set; }

        [Required]
        [StringLength(150)]
        public string SerialNumber { get; set; } = string.Empty;
        public string? Attr1 { get; set; }
        public string? Attr2 { get; set; }
        public string? Attr3 { get; set; }
        public string? Attr4 { get; set; }
        public string? Attr5 { get; set; }
        public string? Attr6 { get; set; }

        public int? StatusId { get; set; }

        [Required]
        public bool IsActive { get; set; } = true;

        [Required]
        public DateTime RegistrationDate { get; set; } = DateTime.Now;
    }
}