using InventoryAPI.Data;
using InventoryAPI.Services.IServices;
using Microsoft.EntityFrameworkCore;

namespace InventoryAPI.Services;

public sealed class CompanyLicenseService(AppDbContext db) : ICompanyLicenseService
{
    public async Task<(bool Allowed, string? Error)> CanCreateUserAsync(
        int companyId,
        CancellationToken cancellationToken = default)
    {
        if (companyId <= 0)
            return (false, "Empresa no válida.");

        // Mantener compatibilidad para las empresas antiguas sin licencia.
        var license = await db.CompanyLicenses.AsNoTracking()
            .FirstOrDefaultAsync(x => x.CompanyId == companyId, cancellationToken);
        if (license is null) return (true, null);
        if (!license.IsEnabled) return (false, "La licencia de la empresa está deshabilitada.");
        if (license.ExpiresAt <= DateTime.UtcNow) return (false, "La licencia de la empresa está vencida.");

        // Max + cupo 0 significa ilimitado. Cero en otros planes no es válido.
        if (license.MaxUsers == 0 && string.Equals(license.Plan, "Max", StringComparison.OrdinalIgnoreCase))
            return (true, null);
        if (license.MaxUsers <= 0)
            return (false, "La licencia tiene un cupo inválido. Contacta al administrador.");

        var activeUsers = await db.Users.CountAsync(
            x => x.CompanyId == companyId && x.IsActive, cancellationToken);
        if (activeUsers >= license.MaxUsers)
            return (false, "Se alcanzó el límite de usuarios permitido por la licencia.");

        return (true, null);
    }
}
