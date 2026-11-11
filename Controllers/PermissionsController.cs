using Microsoft.AspNetCore.Mvc;
using InventoryAPI.Services.IServices;
using InventoryAPI.Security;

namespace InventoryAPI.Controllers
{
    public class PermissionsController(IPermissionService permissionService) : BaseApiController
    {
        private readonly IPermissionService _permissionService = permissionService;

        [HttpGet]
        public async Task<IActionResult> GetPermissions()
        {
            if (!InventoryAccessPolicy.IsDeveloper(User)) return Forbid();
            var permissions = await _permissionService.GetAllAsync();
            return Ok(permissions);
        }
    }
}