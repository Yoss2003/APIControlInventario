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

    public sealed record UserDetailsUpdate(
        [Required, MinLength(3), MaxLength(80)] string Username,
        [Required, EmailAddress, MaxLength(254)] string Email,
        [Required, MaxLength(100)] string FirstName,
        [Required, MaxLength(100)] string LastName,
        [RegularExpression(@"^\d{8}$")] string? Dni);

    [HttpPatch("users/{id:int}/details")]
    public async Task<IActionResult> UpdateDetails(int id, [FromBody] UserDetailsUpdate input)
    {
        if (!await permissions.HasAsync(User, InventoryPermissionCodes.ManageUsers)) return Forbid();
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var user = await db.Users.Include(u => u.Employee)
            .FirstOrDefaultAsync(u => u.Id == id && u.CompanyId == CompanyId);
        if (user is null) return NotFound();
        if (user.RoleId == 1 || (user.RoleId == 2 && !InventoryAccessPolicy.IsDeveloper(User)))
            return Forbid();
        var username = input.Username.Trim();
        var email = input.Email.Trim();
        if (await db.Users.AnyAsync(u => u.Id != id && u.Username == username))
            return Conflict(new { error = "Este usuario ya existe." });
        if (await db.Users.AnyAsync(u => u.Id != id && u.Email == email))
            return Conflict(new { error = "El correo ya existe." });
        if (!string.IsNullOrWhiteSpace(input.Dni) && await db.Employees.AnyAsync(e =>
            e.CompanyId == CompanyId && e.UserId != id && e.DNI == input.Dni))
            return Conflict(new { error = "El DNI ya está registrado." });
        user.Username = username;
        user.Email = email;
        user.Employee ??= new InventoryAPI.Models.Employee
        {
            UserId = user.Id, CompanyId = CompanyId,
            IsActive = true, StatusId = user.StatusId
        };
        user.Employee.FirstName = input.FirstName.Trim();
        user.Employee.LastName = input.LastName.Trim();
        user.Employee.DNI = input.Dni?.Trim();
        await db.SaveChangesAsync();
        return NoContent();
    }

    public sealed record UserPermissionUpdate(
        bool CanApplyDiscounts,
        bool CanViewPurchaseCosts,
        bool CanCancelSales,
        bool CanEditProducts,
        bool CanViewProfits);

    [HttpGet("users/{id:int}/permissions")]
    public async Task<IActionResult> UserPermissions(int id)
    {
        if (id != UserId &&
            !await permissions.HasAsync(User, InventoryPermissionCodes.ManageUsers)) return Forbid();
        var user = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id && u.CompanyId == CompanyId);
        if (user is null) return NotFound();
        var flags = await db.EmployeePermissions.AsNoTracking()
            .FirstOrDefaultAsync(p => p.CompanyId == CompanyId && p.UserId == id);
        var roleCodes = await db.RolePermissions.AsNoTracking()
            .Where(r => r.RoleId == user.RoleId && r.Permission != null)
            .Select(r => r.Permission!.SystemCode).Distinct().ToListAsync();
        return Ok(new
        {
            canApplyDiscounts = flags?.CanApplyDiscounts ?? false,
            canViewPurchaseCosts = flags?.CanViewPurchaseCosts ?? false,
            canCancelSales = flags?.CanCancelSales ?? false,
            canEditProducts = flags?.CanEditProducts ?? false,
            canViewProfits = flags?.CanViewProfits ?? false,
            rolePermissions = user.RoleId is 1 or 2
                ? InventoryPermissionCodes.Definitions.Keys.ToList()
                : roleCodes
        });
    }

    [HttpPut("users/{id:int}/permissions")]
    public async Task<IActionResult> UpdateUserPermissions(int id, [FromBody] UserPermissionUpdate input)
    {
        if (!await permissions.HasAsync(User, InventoryPermissionCodes.ManageUsers)) return Forbid();
        if (id == UserId) return Conflict(new { error = "No puedes cambiar tus propios permisos." });
        var user = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id && u.CompanyId == CompanyId);
        if (user is null) return NotFound();
        if (user.RoleId is 1 or 2) return Forbid();
        var flags = await db.EmployeePermissions.FirstOrDefaultAsync(p =>
            p.CompanyId == CompanyId && p.UserId == id);
        if (flags is null)
        {
            flags = new InventoryAPI.Models.EmployeePermission
                { CompanyId = CompanyId, UserId = id };
            db.EmployeePermissions.Add(flags);
        }
        flags.CanApplyDiscounts = input.CanApplyDiscounts;
        flags.CanViewPurchaseCosts = input.CanViewPurchaseCosts;
        flags.CanCancelSales = input.CanCancelSales;
        flags.CanEditProducts = input.CanEditProducts;
        flags.CanViewProfits = input.CanViewProfits;
        await db.SaveChangesAsync();
        return NoContent();
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
