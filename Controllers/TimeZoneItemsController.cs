using Microsoft.AspNetCore.Mvc;
using InventoryAPI.Services.IServices;

namespace InventoryAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TimeZoneItemsController(ITimeZoneItemService timeZoneItemService) : ControllerBase
    {
        private readonly ITimeZoneItemService _timeZoneItemService = timeZoneItemService;

        // GET: api/TimeZoneItems
        [HttpGet]
        public async Task<IActionResult> GetTimeZones()
        {
            var timeZones = await _timeZoneItemService.GetAllAsync();
            return Ok(timeZones);
        }

        // GET: api/TimeZoneItems/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetTimeZoneItem(int id)
        {
            var timeZoneItem = await _timeZoneItemService.GetByIdAsync(id);

            if (timeZoneItem == null)
            {
                return NotFound();
            }

            return Ok(timeZoneItem);
        }
    }
}