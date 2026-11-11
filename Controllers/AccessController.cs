using InventoryAPI.Data;
using InventoryAPI.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryAPI.Controllers;

/// <summary>Permisos efectivos de la sesión; Angular los usa solo para UX.</summary>
public sealed class AccessController(InventoryAccessPolicy permissions, AppDbContext db) : BaseApiController
{
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var companyId = InventoryAccessPolicy.CompanyId(User);
        var userId = InventoryAccessPolicy.UserId(User);
        if (companyId <= 0 || userId <= 0) return Unauthorized();

        var codes = await permissions.GrantedAsync(User);
        return Ok(new
        {
            userId,
            companyId,
            roleId = InventoryAccessPolicy.RoleId(User),
            isDeveloper = InventoryAccessPolicy.IsDeveloper(User),
            isAdministrator = InventoryAccessPolicy.IsAdmin(User),
            permissions = codes,
            masterInventoryId = await db.Inventories.AsNoTracking()
                .Where(i => i.CompanyId == companyId && i.IsMaster && i.IsActive)
                .Select(i => (int?)i.Id).FirstOrDefaultAsync()
        });
    }
}
