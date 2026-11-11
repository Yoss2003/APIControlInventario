using Microsoft.AspNetCore.Mvc;
using InventoryAPI.Services.IServices;
using InventoryAPI.Models;
using InventoryAPI.Security;
using InventoryAPI.Data;
using Microsoft.EntityFrameworkCore;

namespace InventoryAPI.Controllers
{
    public class EmployeePermissionsController(IEmployeePermissionService service, InventoryAccessPolicy policy, AppDbContext db) : BaseApiController
    {
        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetPermissionsByUser(int userId)
        {
            int companyId = ObtenerEmpresaSegura();
            if (userId != ObtenerUsuarioSeguro() &&
                !await policy.HasAsync(User, InventoryPermissionCodes.ManageUsers)) return Forbid();

            var permissions = await service.GetByUserIdAndCompanyAsync(userId, companyId);
            if (permissions == null) return NotFound();

            return Ok(permissions);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutPermissions(int id, [FromBody] EmployeePermission permissions)
        {
            if (!InventoryAccessPolicy.IsManager(User) ||
                !await policy.HasAsync(User, InventoryPermissionCodes.ManageUsers)) return Forbid();
            if (id != permissions.Id) return BadRequest();

            int companyId = ObtenerEmpresaSegura();
            permissions.CompanyId = companyId;

            var existingPerms = await service.GetByIdAsync(id);
            if (existingPerms == null || existingPerms.CompanyId != companyId) return NotFound();
            if (existingPerms.UserId != permissions.UserId ||
                !await db.Users.AnyAsync(u => u.Id == permissions.UserId &&
                    u.CompanyId == companyId && u.RoleId > 2))
                return Forbid();

            var success = await service.UpdateAsync(permissions);
            if (!success) return BadRequest();

            return NoContent();
        }
    }
}