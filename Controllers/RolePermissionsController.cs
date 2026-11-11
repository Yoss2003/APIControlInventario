using Microsoft.AspNetCore.Mvc;
using InventoryAPI.Services.IServices;
using InventoryAPI.Security;

namespace InventoryAPI.Controllers
{
    public class RolePermissionsController(IRolePermissionService service) : BaseApiController
    {
        [HttpGet]
        public async Task<IActionResult> Get() => InventoryAccessPolicy.IsDeveloper(User)
            ? Ok(await service.GetAllAsync()) : Forbid();
    }
}