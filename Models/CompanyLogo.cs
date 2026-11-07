using System.ComponentModel.DataAnnotations;

namespace InventoryAPI.Models;

/// <summary>Logo binario persistente. Evita depender del disco efímero del hosting.</summary>
public sealed class CompanyLogo
{
    [Key]
    public int CompanyId { get; set; }

    [Required, MaxLength(32)]
    public string ContentType { get; set; } = "image/png";

    [Required]
    public byte[] Content { get; set; } = Array.Empty<byte>();

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
