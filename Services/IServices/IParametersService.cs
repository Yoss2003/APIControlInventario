using ControlInventario.Shared.Models;
using InventoryAPI.Repositories.IRepositories;

namespace InventoryAPI.Services.IServices
{
    public interface IParametersService : IWorkContainer<Parameters>
    {
        new Task<IEnumerable<Parameters>> GetAllByCompanyIdAsync(int companyId);
    }
}