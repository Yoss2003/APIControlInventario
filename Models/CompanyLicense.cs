using System;
using System.ComponentModel.DataAnnotations;

namespace InventoryAPI.Models;

public sealed class CompanyLicense
{
    [Key]
    public int Id { get; set; }
    public int CompanyId { get; set; }

    // Los nuevos registros admiten Básico, Pro, Plus y Max.
    // Los planes históricos permanecen en la base de datos hasta su renovación.
    [MaxLength(40)]
    public string Plan { get; set; } = "Básico";

    // Básico: 5, Pro: 10, Plus: 15, Max: 0 (ilimitado).
    // Cupo contratado, distinto al número de usuarios actualmente registrados.
    public int MaxUsers { get; set; } = 5;

    // Nullable para preservar licencias preexistentes sin fecha de inicio.
    public DateTime? StartsAt { get; set; }

    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddMonths(1);
    public bool IsEnabled { get; set; } = true;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
