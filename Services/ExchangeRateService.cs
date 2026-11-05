using ControlInventario.Shared.Models;
using InventoryAPI.Repositories;
using InventoryAPI.Repositories.IRepositories;
using InventoryAPI.Services.IServices;
using System.Diagnostics;
using System.Text.Json;

namespace InventoryAPI.Services
{
    public class ExchangeRateService(IWorkFlow workFlow, HttpClient httpClient, IConfiguration configuration) : WorkContainer<ExchangeRate>(workFlow), IExchangeRateService
    {
        private readonly HttpClient _httpClient = httpClient;

        public async Task<ExchangeRate> GetTodayExchangeRateAsync(string? currency)
        {
            string monedaBase = string.IsNullOrWhiteSpace(currency) ? "USD" : currency.Trim().ToUpper();
            DateTime hoy = DateTime.UtcNow.Date;

            var listaLocales = await _workFlow.Repository<ExchangeRate>().FindAsync(ex => ex.Date.Date == hoy && ex.BaseCurrency == monedaBase);
            var tcLocal = listaLocales.FirstOrDefault();

            if (tcLocal != null)
            {
                return tcLocal;
            }

            string apiToken = configuration["DecolectaApi:ExchangeToken"] ?? throw new ArgumentNullException("DecolectaApi:ExchangeToken", "El token de Exchange no está configurado.");

            string urlExchange = monedaBase == "EUR" ? "https://api.decolecta.com/v1/tipo-cambio/sbs/accounting?currency=EUR" : "https://api.decolecta.com/v1/tipo-cambio/sunat";

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, urlExchange);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiToken);

                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    string jsonString = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(jsonString);
                    var root = doc.RootElement;

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

                    await _workFlow.Repository<ExchangeRate>().AddAsync(nuevoTc);
                    await _workFlow.CompleteAsync();

                    return nuevoTc;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TC_ERROR] {ex.Message}");
            }

            var historicos = await _workFlow.Repository<ExchangeRate>()
                .FindAsync(ex => ex.BaseCurrency == monedaBase);
            var ultimoTcHistorico = historicos.OrderByDescending(ex => ex.Date).FirstOrDefault();

            if (ultimoTcHistorico != null) return ultimoTcHistorico;

            return new ExchangeRate
            {
                Id = 0,
                BaseCurrency = monedaBase,
                QuoteCurrency = "PEN",
                BuyPrice = monedaBase == "EUR" ? 3.6500m : 3.7400m,
                SellPrice = monedaBase == "EUR" ? 3.7000m : 3.7700m,
                Date = hoy
            };
        }

        private static decimal ObtenerDecimalSeguro(JsonElement element, string propiedad, decimal valorPorDefecto)
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