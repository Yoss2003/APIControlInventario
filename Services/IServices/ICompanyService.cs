using ControlInventario.Shared.Models;
using InventoryAPI.Repositories.IRepositories;

namespace InventoryAPI.Services.IServices
{
    public interface ICompanyService : IWorkContainer<Company>
    {
        Task<IEnumerable<CompanyPublicDTO>> GetActiveCompaniesPublicAsync();
    }
}