using ControlInventario.Shared.Models;
using InventoryAPI.Repositories.IRepositories;

namespace InventoryAPI.Services.IServices
{
    public interface IRoleService : IWorkContainer<Role>
    {
        Task<(bool Success, string Message)> UpdateRolePermissionsAsync(int roleId, List<int> permissionIds);
    }
}