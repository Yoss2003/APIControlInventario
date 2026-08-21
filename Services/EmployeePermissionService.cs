using ControlInventario.Shared.Models;
using InventoryAPI.Repositories;
using InventoryAPI.Repositories.IRepositories;
using InventoryAPI.Services.IServices;

namespace InventoryAPI.Services
{
    public class EmployeePermissionService(IWorkFlow workFlow) : WorkContainer<EmployeePermission>(workFlow), IEmployeePermissionService
    {
        private new readonly IWorkFlow _workFlow = workFlow;

        public async Task<EmployeePermission?> GetByUserIdAndCompanyAsync(int userId, int companyId)
        {
            var allPermissions = await _workFlow.Repository<EmployeePermission>().GetAllAsync();

            return allPermissions.FirstOrDefault(p => p.UserId == userId && p.CompanyId == companyId);
        }
    }
}