using InventoryAPI.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace InventoryAPI.Security;

public static class SessionSecurityValidator
{
    public static async Task ValidateAsync(TokenValidatedContext context)
    {
        var claims = context.Principal;
        if (claims is null ||
            !int.TryParse(claims.FindFirst("UserId")?.Value, out var uid) || uid <= 0 ||
            !int.TryParse(claims.FindFirst("CompanyId")?.Value, out var cid) || cid <= 0 ||
            !int.TryParse(claims.FindFirst("RoleId")?.Value, out var role))
        { context.Fail("Sesión sin ámbito válido."); return; }
        var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == uid);
        if (user is null || !user.IsActive || user.StatusId != 1 || user.RoleId != role ||
            (role != 1 && user.CompanyId != cid))
        { context.Fail("Usuario sin autorización para esta empresa."); return; }
        var company = await db.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cid);
        if (company is null || !company.IsActive)
        { context.Fail("Empresa inactiva."); return; }
        if (role != 1)
        {
            var license = await db.CompanyLicenses.AsNoTracking().FirstOrDefaultAsync(l => l.CompanyId == cid);
            if (license is not null && (!license.IsEnabled || license.ExpiresAt <= DateTime.UtcNow))
            { context.Fail("Licencia inactiva o vencida."); return; }
        }
        // Revoca tokens anteriores a una recuperación aprobada y completada.
        var lastReset = await db.PasswordRecoveryRequests.AsNoTracking()
            .Where(x => x.UserId == uid && x.CompletedAt != null)
            .MaxAsync(x => (DateTime?)x.CompletedAt);
        if (lastReset.HasValue)
        {
            if (!long.TryParse(claims.FindFirst("iat")?.Value, out var issuedAt) ||
                DateTimeOffset.FromUnixTimeSeconds(issuedAt).UtcDateTime.AddSeconds(1) < lastReset.Value)
                context.Fail("La contraseña cambió. Vuelve a iniciar sesión.");
        }
    }
}
