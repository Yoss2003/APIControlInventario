using InventoryAPI.Data;
using InventoryAPI.Models.DTO;
using InventoryAPI.Security;
using InventoryAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace InventoryAPI.Controllers;

/// <summary>Administración por empresa; siempre deriva su ámbito del JWT.</summary>
public sealed class UserManagementController(
    AppDbContext db, InventoryAccessPolicy permissions,
    ManagedUserRegistration registration) : BaseApiController
{
    private int CompanyId => InventoryAccessPolicy.CompanyId(User);
    private int UserId => InventoryAccessPolicy.UserId(User);

    [HttpGet("users")]
    public async Task<IActionResult> ListUsers()
    {
        if (!await permissions.HasAsync(User, InventoryPermissionCodes.ManageUsers)) return Forbid();
        var items = await db.Users.AsNoTracking().Where(u => u.CompanyId == CompanyId)
            .OrderBy(u => u.Username)
            .Select(u => new
            {
                u.Id, u.Username, u.Email, u.RoleId, u.CompanyId,
                u.IsActive, u.StatusId,
                firstName = u.Employee != null ? u.Employee.FirstName : null,
                lastName = u.Employee != null ? u.Employee.LastName : null,
                dni = u.Employee != null ? u.Employee.DNI : null,
                personalInventoryId = db.Inventories.Where(i => i.UserId == u.Id &&
                    i.CompanyId == CompanyId && !i.IsMaster && i.IsActive)
                    .Select(i => (int?)i.Id).FirstOrDefault()
            }).ToListAsync();
        return Ok(items);
    }

    [HttpGet("roles")]
    public async Task<IActionResult> Roles()
    {
        if (!await permissions.HasAsync(User, InventoryPermissionCodes.ManageUsers)) return Forbid();
        return Ok(await db.Roles.AsNoTracking().Where(r => r.Id > 2)
            .Select(r => new { r.Id, r.Name, r.Description })
            .OrderBy(r => r.Name).ToListAsync());
    }

    [HttpPost("users")]
    public async Task<IActionResult> CreateUser([FromBody] ManagedUserInput input)
    {
        if (!await permissions.HasAsync(User, InventoryPermissionCodes.ManageUsers)) return Forbid();
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var result = await registration.RegisterAsync(CompanyId, input, firstAdministrator: false);
        return result.Success ? Ok(result.Created) : Conflict(new { error = result.Error });
    }

    public sealed record UserAccessUpdate(bool IsActive, int? RoleId);

    [HttpPatch("users/{id:int}/access")]
    public async Task<IActionResult> UpdateAccess(int id, [FromBody] UserAccessUpdate input)
    {
        if (!await permissions.HasAsync(User, InventoryPermissionCodes.ManageUsers)) return Forbid();
        if (id == UserId) return Conflict(new { error = "No puedes modificar tu propio estado o rol." });
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id && u.CompanyId == CompanyId);
        if (user is null) return NotFound();
        if (user.RoleId == 1 || user.RoleId == 2) return Forbid();
        if (input.RoleId.HasValue)
        {
            if (input.RoleId <= 2 || !await db.Roles.AnyAsync(r => r.Id == input.RoleId.Value))
                return BadRequest(new { error = "Rol inválido." });
            user.RoleId = input.RoleId.Value;
        }
        user.IsActive = input.IsActive;
        await db.SaveChangesAsync();
        return NoContent();
    }
}
