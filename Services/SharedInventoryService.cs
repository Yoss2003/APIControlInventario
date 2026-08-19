using ControlInventario.Shared.Models;
using InventoryAPI.Data;
using InventoryAPI.Repositories;
using InventoryAPI.Repositories.IRepositories;
using InventoryAPI.Services.IServices;
using Microsoft.EntityFrameworkCore;

namespace InventoryAPI.Services
{
    public class SharedInventoryService(IWorkFlow workFlow, AppDbContext context) : WorkContainer<SharedInventory>(workFlow), ISharedInventoryService
    {
        public async Task<List<SharedInventoryDTO>> GetSharedWithUsersAsync(int inventoryId)
        {
            return await context.SharedInventories
                .Include(s => s.User)
                .Include(s => s.Inventory)
                .Where(s => s.InventoryId == inventoryId)
                .Select(s => new SharedInventoryDTO
                {
                    Id = s.Id,
                    InventoryId = s.InventoryId,
                    UserId = s.UserId,
                    Username = s.User != null ? s.User.Username! : "Desconocido",
                    AccessLevel = s.AccessLevel,
                    SharedDate = s.SharedDate,
                    GrantedBy = s.Inventory != null ? s.Inventory.Username : "Administrador"
                })
                .ToListAsync();
        }
    }
}