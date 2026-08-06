using ControlInventario.Shared.Models;
using InventoryAPI.Repositories.IRepositories;

namespace InventoryAPI.Services.IServices
{
    public interface IExchangeRateService : IWorkContainer<ExchangeRate>
    {
        Task<ExchangeRate> GetTodayExchangeRateAsync(string? currency);
    }
}