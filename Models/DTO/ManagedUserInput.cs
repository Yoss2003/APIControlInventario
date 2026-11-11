using System.ComponentModel.DataAnnotations;

namespace InventoryAPI.Models.DTO;

/// <summary>Alta administrada, nunca enlaza propiedades privilegiadas del modelo User.</summary>
public sealed class ManagedUserInput
{
    [Required, StringLength(80, MinimumLength = 3)]
    public string Username { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(254)]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(12), MaxLength(128)]
    public string InitialPassword { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [RegularExpression(@"^\d{8}$")]
    public string? Dni { get; set; }

    public int RoleId { get; set; }
}
