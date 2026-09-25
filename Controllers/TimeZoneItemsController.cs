using Microsoft.AspNetCore.Mvc;
using InventoryAPI.Services.IServices;
namespace InventoryAPI.Controllers
{
    public class TimeZoneItemsController(ITimeZoneItemService service) : BaseApiController
    {
        [HttpGet] public async Task<IActionResult> GetTimeZones() => Ok(await service.GetAllAsync());
        [HttpGet("{id}")]
        public async Task<IActionResult> GetTimeZoneItem(int id)
        {
            var item = await service.GetByIdAsync(id);
            return item == null ? NotFound() : Ok(item);
        }
    }
}