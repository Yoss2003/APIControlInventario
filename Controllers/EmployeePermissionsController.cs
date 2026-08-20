using Microsoft.AspNetCore.Mvc;
using InventoryAPI.Services.IServices;
using ControlInventario.Shared.Models;

namespace InventoryAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EmployeePermissionsController(IEmployeePermissionService service) : ControllerBase
    {
        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetPermissionsByUser(int userId)
        {
            // 🚀 CANDADO: ¿A qué sucursal le estamos consultando los permisos?
            if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader))
                return BadRequest("Falta indicar la sucursal.");
            int companyId = int.Parse(companyIdHeader!);

            // Tu servicio debería buscar usando AMBOS parámetros (UserId y CompanyId)
            var permissions = await service.GetByUserIdAndCompanyAsync(userId, companyId);

            if (permissions == null) return NotFound();
            return Ok(permissions);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutPermissions(int id, [FromBody] EmployeePermission permissions)
        {
            if (id != permissions.Id) return BadRequest();
            if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader)) return BadRequest("Falta indicar la sucursal.");

            int companyId = int.Parse(companyIdHeader!);
            permissions.CompanyId = companyId; // Forzamos la empresa

            var existingPerms = await service.GetByIdAsync(id);
            if (existingPerms == null || existingPerms.CompanyId != companyId) return NotFound();

            var success = await service.UpdateAsync(permissions);
            if (!success) return BadRequest();

            return NoContent();
        }
    }
}