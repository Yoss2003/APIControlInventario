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

            int companyId;
            if (Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader))
            {
                companyId = int.Parse(companyIdHeader!);
            }
            else if (nuevaVenta.CompanyId > 0)
            {
                companyId = nuevaVenta.CompanyId;
            }
            else
            {
                return BadRequest(new { Message = "Falta indicar la empresa (X-Company-Id)." });
            }

            nuevaVenta.CompanyId = companyId;
            if (nuevaVenta.SaleDetails != null)
            {
                foreach (var detail in nuevaVenta.SaleDetails)
                {
                    detail.CompanyId = companyId;
                }
            }

            var result = await _saleService.ProcessSaleAsync(nuevaVenta);
            if (!result.Success)
            {
                if (result.Message.Contains("crítico")) return StatusCode(500, new { Message = result.Message });
                return BadRequest(new { Message = result.Message });
            }
            return Ok(new { result.Message });
        }
    }
}