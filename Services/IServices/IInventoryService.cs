using ControlInventario.Shared.Models;
using InventoryAPI.Repositories.IRepositories;
using InventoryAPI.Models.DTO;

namespace InventoryAPI.Services.IServices
{
    public interface IInventoryService : IWorkContainer<Inventory>
    {
        Task<(bool Success, string Message)> ShareInventoryAsync(ShareRequestDTO request);
    }
}