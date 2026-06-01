using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InventoryAPI.Data;
using ControlInventario.Shared.Models;
using System.Text.Json;

namespace InventoryAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ExchangeRatesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly HttpClient _httpClient;

        // El constructor recibe el contexto de la BD y el HttpClient gracias a la configuración de tu Program.cs
        public ExchangeRatesController(AppDbContext context, HttpClient httpClient)
        {
            _context = context;
            _httpClient = httpClient;
        }

        // ====================================================================
        // 🌟 ENDPOINT CENTRAL: OBTENER EL TIPO DE CAMBIO DE HOY (CON CACHÉ LOCAL)
        // ====================================================================
        // GET: api/ExchangeRates/today
        [HttpGet("today/{currency?}")]
        public async Task<IActionResult> GetTodayExchangeRate(string? currency)
        {
            // Si viene vacío, por defecto rastreamos Dólares (USD)
            string monedaBase = string.IsNullOrWhiteSpace(currency) ? "USD" : currency.Trim().ToUpper();
            DateTime hoy = DateTime.Today;

            try
            {
                // 1. CACHÉ LOCAL: Buscamos si ya guardamos la cotización de esa moneda hoy en Somee
                var tcLocal = await _context.ExchangeRates
                    .FirstOrDefaultAsync(ex => ex.Date.Date == hoy && ex.BaseCurrency == monedaBase);

                if (tcLocal != null)
                {
                    return Ok(tcLocal);
                }

                // 2. INTERNET: Si no está en BD, disparamos la consulta externa
                string apiToken = "sk_13723.G2u2hnJk9acgY3uFHZMYsJliHho0GXu4";
                string urlExchange = "";

                if (monedaBase == "EUR")
                {
                    // Endpoint oficial de la SBS Contable de Decolecta para el Euro
                    urlExchange = "https://api.decolecta.com/v1/tipo-cambio/sbs/accounting?currency=EUR";
                }
                else
                {
                    // Endpoint tradicional de la SUNAT para el Dólar
                    urlExchange = "https://api.decolecta.com/v1/tipo-cambio/sunat";
                }

                var request = new HttpRequestMessage(HttpMethod.Get, urlExchange);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiToken);

                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    string jsonString = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(jsonString);
                    var root = doc.RootElement;

                    // Extracción elástica y segura de los precios
                    decimal compra = ObtenerDecimalSeguro(root, "buy_price", monedaBase == "EUR" ? 3.65m : 3.75m);
                    decimal venta = ObtenerDecimalSeguro(root, "sell_price", monedaBase == "EUR" ? 3.70m : 3.78m);

                    var nuevoTc = new ExchangeRate
                    {
                        BaseCurrency = monedaBase,
                        QuoteCurrency = "PEN",
                        BuyPrice = compra,
                        SellPrice = venta,
                        Date = hoy
                    };

                    _context.ExchangeRates.Add(nuevoTc);
                    await _context.SaveChangesAsync();

                    return Ok(nuevoTc);
                }

                // 3. SALVAVIDAS HISTÓRICO: Si la API externa falla, pescamos el último registro de esa moneda
                var ultimoTcHistorico = await _context.ExchangeRates
                    .Where(ex => ex.BaseCurrency == monedaBase)
                    .OrderByDescending(ex => ex.Date)
                    .FirstOrDefaultAsync();

                if (ultimoTcHistorico != null) return Ok(ultimoTcHistorico);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TC_ERROR] {ex.Message}");
            }

            // 4. SALVAVIDAS FINAL POR DEFECTO (Evita congelamientos si todo falla)
            var tcPorDefecto = new ExchangeRate
            {
                Id = 0,
                BaseCurrency = monedaBase,
                QuoteCurrency = "PEN",
                BuyPrice = monedaBase == "EUR" ? 3.6500m : 3.7400m,
                SellPrice = monedaBase == "EUR" ? 3.7000m : 3.7700m,
                Date = hoy
            };

            return Ok(tcPorDefecto);
        }

        private decimal ObtenerDecimalSeguro(JsonElement element, string propiedad, decimal valorPorDefecto)
        {
            if (element.TryGetProperty(propiedad, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.Number) return prop.GetDecimal();
                if (prop.ValueKind == JsonValueKind.String && decimal.TryParse(prop.GetString(), out var parsedVal))
                {
                    return parsedVal;
                }
            }
            return valorPorDefecto;
        }
    }
}