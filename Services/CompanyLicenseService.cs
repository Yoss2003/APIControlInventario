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

        // Preserva el comportamiento anterior de la API para empresas sin licencia creada.
        var license = await db.CompanyLicenses.AsNoTracking()
            .FirstOrDefaultAsync(x => x.CompanyId == companyId, cancellationToken);

        if (license is null)
            return (true, null);

        if (!license.IsEnabled)
            return (false, "La licencia de la empresa está deshabilitada.");

        if (license.ExpiresAt <= DateTime.UtcNow)
            return (false, "La licencia de la empresa está vencida.");

        var activeUsers = await db.Users.CountAsync(
            x => x.CompanyId == companyId && x.IsActive,
            cancellationToken);

        if (activeUsers >= license.MaxUsers)
            return (false, "Se alcanzó el límite de usuarios permitido por la licencia.");

        return (true, null);
    }
}
