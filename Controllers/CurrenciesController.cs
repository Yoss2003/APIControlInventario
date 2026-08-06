using Microsoft.AspNetCore.Mvc;
using InventoryAPI.Services.IServices;
using ControlInventario.Shared.Models;

namespace InventoryAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CurrenciesController(ICurrencyService currencyService) : ControllerBase
    {
        private readonly ICurrencyService _currencyService = currencyService;

        // GET: api/Currencies
        [HttpGet]
        public async Task<IActionResult> GetCurrencies()
        {
            try
            {
                var currencies = await _currencyService.GetAllAsync();
                return Ok(currencies);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno del servidor al recuperar las monedas: {ex.Message}");
            }
        }

        // GET: api/Currencies/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetCurrency(int id)
        {
            try
            {
                var currency = await _currencyService.GetByIdAsync(id);

                if (currency == null)
                {
                    return NotFound($"No se encontró la moneda con ID {id}.");
                }

                return Ok(currency);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno del servidor al recuperar la moneda: {ex.Message}");
            }
        }
    }
}