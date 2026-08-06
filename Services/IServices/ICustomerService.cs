using ControlInventario.Shared.Models;
using InventoryAPI.Repositories.IRepositories;

namespace InventoryAPI.Services.IServices
{
    public interface ICustomerService : IWorkContainer<Customer>
    {
        Task<(bool IsSuccess, string DataOrError)> ConsultarDniExternoAsync(string dni);
    }
}