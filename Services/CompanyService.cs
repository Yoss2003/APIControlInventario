using ControlInventario.Shared.Models;
using InventoryAPI.Repositories;
using InventoryAPI.Repositories.IRepositories;
using InventoryAPI.Services.IServices;

namespace InventoryAPI.Services
{
    public class CompanyService(IWorkFlow workFlow) : WorkContainer<Company>(workFlow), ICompanyService
    {
        public async Task<IEnumerable<CompanyPublicDTO>> GetActiveCompaniesPublicAsync()
        {
            var companies = await _workFlow.Repository<Company>().GetAllAsync();

            return companies
                .Where(c => c.IsActive)
                .Select(c => new CompanyPublicDTO
                {
                    Id = c.Id,
                    BusinessName = c.BusinessName,
                    LogoUrl = c.LogoUrl,
                    PrimaryColorHex = c.PrimaryColorHex
                })
                .ToList();
        }
    }
}