using InventoryAPI.Data;
using InventoryAPI.Models.DTO;
using InventoryAPI.Security;
using InventoryAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryAPI.Controllers;

/// <summary>Bootstrap exclusivo de desarrollador para la primera cuenta administradora.</summary>
[Route("api/dev/companies")]
public sealed class DevCompanyAdminsController(
    AppDbContext db, ManagedUserRegistration registration) : BaseApiController
{
    [HttpPost("{companyId:int}/first-admin")]
    public async Task<IActionResult> CreateFirstAdmin(
        int companyId, [FromBody] ManagedUserInput input)
    {
        if (!InventoryAccessPolicy.IsDeveloper(User)) return Forbid();
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        if (input.RoleId != 2)
            return BadRequest(new { error = "Selecciona el rol Administrador (2)." });
        if (!await db.Companies.AsNoTracking().AnyAsync(c => c.Id == companyId && c.IsActive))
            return NotFound(new { error = "Empresa no encontrada." });

        var result = await registration.RegisterAsync(companyId, input, firstAdministrator: true);
        return result.Success ? Ok(result.Created) : Conflict(new { error = result.Error });
    }
}
