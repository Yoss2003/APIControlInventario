using Microsoft.AspNetCore.Mvc;
using InventoryAPI.Services.IServices;
using ControlInventario.Shared.Models;

namespace InventoryAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SalesController(ISaleService saleService) : ControllerBase
    {
        private readonly ISaleService _saleService = saleService;

        [HttpPost]
        public async Task<IActionResult> CreateSale([FromBody] Sale nuevaVenta)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _saleService.ProcessSaleAsync(nuevaVenta);

            if (!result.Success)
            {
                // Manejamos códigos de estado según el tipo de error
                if (result.Message.Contains("crítico"))
                {
                    return StatusCode(500, new { Message = result.Message });
                }
                return BadRequest(new { Message = result.Message });
            }

            return Ok(new { Message = result.Message });
        }
    }
}