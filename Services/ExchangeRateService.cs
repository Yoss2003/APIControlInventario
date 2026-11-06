using InventoryAPI.Models;
using InventoryAPI.Repositories;
using InventoryAPI.Repositories.IRepositories;
using InventoryAPI.Services.IServices;
using System.Net.Http.Headers;
using System.Text.Json;

namespace InventoryAPI.Services;

public sealed class ExchangeRateService(IWorkFlow workFlow, HttpClient httpClient, IConfiguration configuration)
    : WorkContainer<ExchangeRate>(workFlow), IExchangeRateService
{
    public async Task<ExchangeRate> GetTodayExchangeRateAsync(string? currency)
    {
        string code = string.IsNullOrWhiteSpace(currency) ? "USD" : currency.Trim().ToUpperInvariant();
        if (code is not ("USD" or "EUR")) throw new ArgumentException("Solo USD o EUR.");
        var today = DateTime.UtcNow.Date;
        var cache = (await _workFlow.Repository<ExchangeRate>().FindAsync(x => x.BaseCurrency == code && x.Date.Date == today))
            .FirstOrDefault();
        if (cache != null) return cache;

        var token = configuration["DecolectaApi:ExchangeToken"];
        if (!string.IsNullOrWhiteSpace(token))
        {
            try
            {
                var url = code == "EUR" ? "https://api.decolecta.com/v1/tipo-cambio/sbs/accounting?currency=EUR"
                    : "https://api.decolecta.com/v1/tipo-cambio/sunat";
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                using var response = await httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    using var data = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                    var root = data.RootElement;
                    if (TryRate(root, "buy_price", out decimal buy) && TryRate(root, "sell_price", out decimal sell) && buy > 0 && sell > 0)
                    {
                        var record = new ExchangeRate { BaseCurrency = code, QuoteCurrency = "PEN",
                            BuyPrice = buy, SellPrice = sell, Date = today };
                        await _workFlow.Repository<ExchangeRate>().AddAsync(record);
                        await _workFlow.CompleteAsync();
                        return record;
                    }
                }
            }
            catch (Exception) { /* Usar únicamente historial real, nunca tasas inventadas. */ }
        }
        var history = await _workFlow.Repository<ExchangeRate>().FindAsync(x => x.BaseCurrency == code);
        var previous = history.OrderByDescending(x => x.Date).FirstOrDefault();
        if (previous != null) return previous; // Fecha explícita: Angular muestra si es histórico.
        throw new InvalidOperationException("No hay una cotización verificada disponible para " + code);
    }
    private static bool TryRate(JsonElement root, string key, out decimal value)
    {
        value = 0;
        if (!root.TryGetProperty(key, out var prop)) return false;
        if (prop.ValueKind == JsonValueKind.Number) return prop.TryGetDecimal(out value);
        return prop.ValueKind == JsonValueKind.String && decimal.TryParse(prop.GetString(),
            System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture, out value);
    }
}
