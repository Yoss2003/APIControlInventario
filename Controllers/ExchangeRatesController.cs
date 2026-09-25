using Microsoft.AspNetCore.Mvc;
using InventoryAPI.Services.IServices;

namespace InventoryAPI.Controllers
{
    public class ExchangeRatesController(IExchangeRateService exchangeRateService) : BaseApiController
    {
        private readonly IExchangeRateService _exchangeRateService = exchangeRateService;

        [HttpGet]
        public async Task<IActionResult> GetExchangeRates()
        {
            try
            {
                var rates = await _exchangeRateService.GetAllAsync();
                return Ok(rates);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [HttpGet("today/{currency?}")]
        public async Task<IActionResult> GetTodayExchangeRate(string? currency)
        {
            try
            {
                var exchangeRate = await _exchangeRateService.GetTodayExchangeRateAsync(currency);
                return Ok(exchangeRate);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Error interno al procesar el tipo de cambio.", detalle = ex.Message });
            }
        }
    }
}