using ControlInventario.Shared.Models;
using InventoryAPI.Repositories;
using InventoryAPI.Repositories.IRepositories;
using InventoryAPI.Services.IServices;

namespace InventoryAPI.Services
{
    public class ParametersService(IWorkFlow workFlow) : WorkContainer<Parameters>(workFlow), IParametersService
    {
        public new async Task<IEnumerable<Parameters>> GetAllByCompanyIdAsync(int companyId)
        {
            return await _workFlow.Parameters.FindAsync(p => p.CompanyId == companyId || p.CompanyId == 1);
        }
    }
}