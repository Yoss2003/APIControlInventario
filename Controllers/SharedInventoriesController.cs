using Microsoft.AspNetCore.Mvc;
using InventoryAPI.Services.IServices;

namespace InventoryAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SharedInventoriesController(ISharedInventoryService service) : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader)) return BadRequest("Falta indicar la sucursal.");
            int companyId = int.Parse(companyIdHeader!);

            return Ok(await service.GetAllByCompanyIdAsync(companyId));
        }

        [HttpGet("inventory/{inventoryId}")]
        public async Task<IActionResult> GetByInventory(int inventoryId)
        {
            // Solo exigimos la cabecera para estandarizar la entrada
            if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader)) return BadRequest("Falta indicar la sucursal.");

            var result = await service.GetSharedWithUsersAsync(inventoryId);
            return Ok(result);
        }
    }
}