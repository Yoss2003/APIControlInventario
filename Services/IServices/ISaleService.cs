using ControlInventario.Shared.Models;
using InventoryAPI.Repositories.IRepositories;

namespace InventoryAPI.Services.IServices
{
    public interface ISaleService : IWorkContainer<Sale>
    {
        Task<(bool Success, string Message)> ProcessSaleAsync(Sale nuevaVenta);
    }
}