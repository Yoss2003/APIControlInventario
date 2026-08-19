using ControlInventario.Shared.Models;
using InventoryAPI.Repositories.IRepositories;

namespace InventoryAPI.Services.IServices
{
    public interface ISharedInventoryService : IWorkContainer<SharedInventory>
    {
        Task<List<SharedInventoryDTO>> GetSharedWithUsersAsync(int inventoryId);
    }
}