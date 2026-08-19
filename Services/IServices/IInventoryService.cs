using ControlInventario.Shared.Models;
using ControlInventario.Shared.Models.DTO;
using InventoryAPI.Repositories.IRepositories;

namespace InventoryAPI.Services.IServices
{
    public interface IInventoryService : IWorkContainer<Inventory>
    {
        Task<(bool Success, string Message)> ShareInventoryAsync(ShareRequestDTO request);
        Task<IEnumerable<SharedInventory>> GetSharedInventoriesAsync(int inventoryId);
        Task<bool> RevokeAccessAsync(int sharedInventoryId);
    }
}