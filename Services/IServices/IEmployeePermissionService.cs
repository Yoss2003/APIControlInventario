using ControlInventario.Shared.Models;
using InventoryAPI.Repositories.IRepositories;

namespace InventoryAPI.Services.IServices
{
    public interface IEmployeePermissionService : IWorkContainer<EmployeePermission>
    {
        Task<EmployeePermission?> GetByUserIdAndCompanyAsync(int userId, int companyId);
    }
}