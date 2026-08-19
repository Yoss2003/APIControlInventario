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

        [HttpGet]
        public async Task<IActionResult> GetSales()
        {
            try
            {
                if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader)) return BadRequest("Falta indicar la sucursal.");
                int companyId = int.Parse(companyIdHeader!);

                var ventas = await _saleService.GetAllByCompanyIdAsync(companyId);
                return Ok(ventas);
            }
            catch (Exception ex) { return StatusCode(500, $"Error interno: {ex.Message}"); }
        }

        [HttpPost]
        public async Task<IActionResult> CreateSale([FromBody] Sale nuevaVenta)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            if (Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader))
                nuevaVenta.CompanyId = int.Parse(companyIdHeader!);

            var result = await _saleService.ProcessSaleAsync(nuevaVenta);
            if (!result.Success)
            {
                if (result.Message.Contains("crítico")) return StatusCode(500, new { Message = result.Message });
                return BadRequest(new { Message = result.Message });
            }
            return Ok(new { Message = result.Message });
        }
    }
}