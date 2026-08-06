using ControlInventario.Shared.Models;
using InventoryAPI.Repositories.IRepositories;

namespace InventoryAPI.Services.IServices
{
    public interface IMovementService : IWorkContainer<Movement>
    {
    }
}