using ControlInventario.Shared.Models;
using InventoryAPI.Repositories.IRepositories;

namespace InventoryAPI.Services.IServices
{
    public interface ISupplierService : IWorkContainer<Supplier>
    {
        Task<(bool Success, Supplier? Data, string Message)> ConsultarRucAsync(string ruc);
    }
}