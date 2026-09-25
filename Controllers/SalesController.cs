using Microsoft.AspNetCore.Mvc;
using InventoryAPI.Services.IServices;
using ControlInventario.Shared.Models;

namespace InventoryAPI.Controllers
{
    public class SalesController(ISaleService saleService) : BaseApiController
    {
        private readonly ISaleService _saleService = saleService;

        [HttpGet]
        public async Task<IActionResult> GetSales()
        {
            try
            {
                int companyId = ObtenerEmpresaSegura();
                var ventas = await _saleService.GetAllByCompanyIdAsync(companyId);
                return Ok(ventas);
            }
            catch (Exception ex) { return StatusCode(500, $"Error interno: {ex.Message}"); }
        }

        [HttpPost]
        public async Task<IActionResult> CreateSale([FromBody] Sale nuevaVenta)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            int companyId = ObtenerEmpresaSegura();
            nuevaVenta.CompanyId = companyId;

            if (nuevaVenta.SaleDetails != null)
            {
                foreach (var detail in nuevaVenta.SaleDetails)
                {
                    detail.CompanyId = companyId;
                }
            }

            var (Success, Message) = await _saleService.ProcessSaleAsync(nuevaVenta);
            if (!Success)
            {
                if (Message.Contains("crítico")) return StatusCode(500, new { Message });
                return BadRequest(new { Message });
            }

            return Ok(new { Message });
        }
    }
}