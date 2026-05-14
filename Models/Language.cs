using System.ComponentModel.DataAnnotations;

namespace InventoryAPI.Models
{
    public class Language
    {
        [Key]
        public int Id { get; set; }

        [StringLength(100)]
        public string? LanguageName { get; set; }
    }
}