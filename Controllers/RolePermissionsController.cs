using Microsoft.AspNetCore.Mvc;
using InventoryAPI.Services.IServices;

namespace InventoryAPI.Controllers
{
    public class RolePermissionsController(IRolePermissionService service) : BaseApiController
    {
        [HttpGet]
        public async Task<IActionResult> Get() => Ok(await service.GetAllAsync());
    }
}