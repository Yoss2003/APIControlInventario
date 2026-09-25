using Microsoft.AspNetCore.Mvc;
using InventoryAPI.Services.IServices;

namespace InventoryAPI.Controllers
{
    public class SharedInventoriesController(ISharedInventoryService service) : BaseApiController
    {
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            int companyId = ObtenerEmpresaSegura();
            return Ok(await service.GetAllByCompanyIdAsync(companyId));
        }

        [HttpGet("inventory/{inventoryId}")]
        public async Task<IActionResult> GetByInventory(int inventoryId)
        {
            var result = await service.GetSharedWithUsersAsync(inventoryId);
            return Ok(result);
        }
    }
}