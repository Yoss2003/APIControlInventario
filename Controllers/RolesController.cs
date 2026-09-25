using Microsoft.AspNetCore.Mvc;
using InventoryAPI.Services.IServices;

namespace InventoryAPI.Controllers
{
    public class RolesController(IRoleService roleService) : BaseApiController
    {
        private readonly IRoleService _roleService = roleService;

        [HttpGet]
        public async Task<IActionResult> GetRoles() => Ok(await _roleService.GetAllAsync());

        [HttpGet("{id}")]
        public async Task<IActionResult> GetRole(int id)
        {
            var role = await _roleService.GetByIdAsync(id);
            if (role == null) return NotFound();

            return Ok(role);
        }

        [HttpPost("{id}/permissions")]
        public async Task<IActionResult> UpdateRolePermissions(int id, [FromBody] List<int> permissionIds)
        {
            var (Success, Message) = await _roleService.UpdateRolePermissionsAsync(id, permissionIds);
            if (!Success) return NotFound(new { message = Message });

            return Ok(new { message = Message });
        }
    }
}