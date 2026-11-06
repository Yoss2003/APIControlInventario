using System.ComponentModel.DataAnnotations;

namespace InventoryAPI.Models;

public sealed class CompanyLicense
{
    [Key]
    public int Id { get; set; }
    public int CompanyId { get; set; }
    [MaxLength(40)] public string Plan { get; set; } = "Basico";
    public int MaxUsers { get; set; } = 5;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddMonths(1);
    public bool IsEnabled { get; set; } = true;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
