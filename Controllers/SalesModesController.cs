using Microsoft.AspNetCore.Mvc;
using InventoryAPI.Services.IServices;
namespace InventoryAPI.Controllers
{
    public class SalesModesController(ISalesModeService service) : BaseApiController
    {
        [HttpGet] public async Task<IActionResult> GetSalesModes() => Ok(await service.GetAllAsync());
        [HttpGet("{id}")]
        public async Task<IActionResult> GetSalesMode(int id)
        {
            var mode = await service.GetByIdAsync(id);
            return mode == null ? NotFound() : Ok(mode);
        }
    }
}