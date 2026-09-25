using Microsoft.AspNetCore.Mvc;
using InventoryAPI.Services.IServices;

namespace InventoryAPI.Controllers
{
    public class PermissionsController(IPermissionService permissionService) : BaseApiController
    {
        private readonly IPermissionService _permissionService = permissionService;

        [HttpGet]
        public async Task<IActionResult> GetPermissions()
        {
            var permissions = await _permissionService.GetAllAsync();
            return Ok(permissions);
        }
    }
}