using System.Security.Claims;
using InventoryAPI.Data;
using Microsoft.EntityFrameworkCore;

namespace InventoryAPI.Security;

/// <summary>
/// Defensa de autorización que se ejecuta en la API.
/// Los roles 1/2 conservan sus atribuciones existentes, pero todas las operaciones
/// siguen sujetas al ámbito empresarial del JWT y las reglas del controlador.
/// </summary>
public sealed class InventoryAccessPolicy(AppDbContext db)
{
    public static int RoleId(ClaimsPrincipal principal) =>
        int.TryParse(principal.FindFirst("RoleId")?.Value, out var value) ? value : 0;

    public static int CompanyId(ClaimsPrincipal principal) =>
        int.TryParse(principal.FindFirst("CompanyId")?.Value, out var value) ? value : 0;

    public static int UserId(ClaimsPrincipal principal) =>
        int.TryParse(principal.FindFirst("UserId")?.Value, out var value) ? value : 0;

    public static bool IsDeveloper(ClaimsPrincipal principal) => RoleId(principal) == 1;
    public static bool IsAdmin(ClaimsPrincipal principal) => RoleId(principal) == 2;
    public static bool IsManager(ClaimsPrincipal principal) => RoleId(principal) is 1 or 2;

    public async Task<bool> HasAsync(ClaimsPrincipal principal, string code)
    {
        var role = RoleId(principal);
        if (role is 1 or 2) return true;
        if (role <= 0 || CompanyId(principal) <= 0) return false;
        return await db.RolePermissions.AsNoTracking().AnyAsync(p =>
            p.RoleId == role && p.Permission != null &&
            p.Permission.SystemCode == code);
    }

    public async Task<string[]> GrantedAsync(ClaimsPrincipal principal)
    {
        var role = RoleId(principal);
        if (role is 1 or 2) return InventoryPermissionCodes.Definitions.Keys.ToArray();
        if (role <= 0) return [];
        return await db.RolePermissions.AsNoTracking()
            .Where(p => p.RoleId == role && p.Permission != null)
            .Select(p => p.Permission!.SystemCode).Distinct().ToArrayAsync();
    }
}
