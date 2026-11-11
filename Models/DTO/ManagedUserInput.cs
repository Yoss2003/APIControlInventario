using System.ComponentModel.DataAnnotations;

namespace InventoryAPI.Models.DTO;

/// <summary>Alta administrada, nunca enlaza propiedades privilegiadas del modelo User.</summary>
public sealed class ManagedUserInput
{
    // La API genera el nombre con la regla del proyecto MAUI.
    // El texto enviado por el navegador es una sugerencia sin autoridad.
    public string? Username { get; set; }

    [Required, EmailAddress, MaxLength(254)]
    public string Email { get; set; } = string.Empty;

    // Las credenciales temporales se generan exclusivamente en el servidor.
    [Required, MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [RegularExpression(@"^\d{8}$")]
    public string? Dni { get; set; }

    public int RoleId { get; set; }
}
