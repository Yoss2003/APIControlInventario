using Microsoft.AspNetCore.Mvc;
using InventoryAPI.Services.IServices;
using System;
using System.Threading.Tasks;

namespace InventoryAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ExchangeRatesController(IExchangeRateService exchangeRateService) : ControllerBase
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