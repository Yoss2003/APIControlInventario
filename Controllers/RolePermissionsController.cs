using Microsoft.AspNetCore.Mvc;
using InventoryAPI.Services.IServices;
namespace InventoryAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RolePermissionsController(IRolePermissionService service) : ControllerBase
    {
        [HttpGet] public async Task<IActionResult> Get() => Ok(await service.GetAllAsync());
    }
}