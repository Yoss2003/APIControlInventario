using Microsoft.AspNetCore.Mvc;
using InventoryAPI.Services.IServices;

namespace InventoryAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SalesModesController(ISalesModeService salesModeService) : ControllerBase
    {
        private readonly ISalesModeService _salesModeService = salesModeService;

        // GET: api/SalesModes
        [HttpGet]
        public async Task<IActionResult> GetSalesModes()
        {
            var modes = await _salesModeService.GetAllAsync();
            return Ok(modes);
        }

        // GET: api/SalesModes/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetSalesMode(int id)
        {
            var mode = await _salesModeService.GetByIdAsync(id);

            if (mode == null)
            {
                return NotFound();
            }

            return Ok(mode);
        }
    }
}