using Microsoft.AspNetCore.Mvc;
using InventoryAPI.Services.IServices;
using ControlInventario.Shared.Models;

namespace InventoryAPI.Controllers
{
    public class EmployeePermissionsController(IEmployeePermissionService service) : BaseApiController
    {
        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetPermissionsByUser(int userId)
        {
            int companyId = ObtenerEmpresaSegura();

            var permissions = await service.GetByUserIdAndCompanyAsync(userId, companyId);
            if (permissions == null) return NotFound();

            return Ok(permissions);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutPermissions(int id, [FromBody] EmployeePermission permissions)
        {
            if (id != permissions.Id) return BadRequest();

            int companyId = ObtenerEmpresaSegura();
            permissions.CompanyId = companyId;

            var existingPerms = await service.GetByIdAsync(id);
            if (existingPerms == null || existingPerms.CompanyId != companyId) return NotFound();

            var success = await service.UpdateAsync(permissions);
            if (!success) return BadRequest();

            return NoContent();
        }
    }
}