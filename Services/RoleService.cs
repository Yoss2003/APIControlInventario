using ControlInventario.Shared.Models;
using InventoryAPI.Repositories;
using InventoryAPI.Repositories.IRepositories;
using InventoryAPI.Services.IServices;

namespace InventoryAPI.Services
{
    public class RoleService(IWorkFlow workFlow) : WorkContainer<Role>(workFlow), IRoleService
    {
        // Sobrescribimos el GetAllAsync para incluir los permisos automáticamente
        public override async Task<IEnumerable<Role>> GetAllAsync()
        {
            return await _workFlow.Repository<Role>().GetAllWithIncludeAsync(r => r.RolePermissions!);
        }

        public async Task<(bool Success, string Message)> UpdateRolePermissionsAsync(int roleId, List<int> permissionIds)
        {
            var rol = await _workFlow.Repository<Role>().GetByIdAsync(roleId);
            if (rol == null)
            {
                return (false, "El rol no existe.");
            }

            // 1. Buscamos y eliminamos los permisos actuales del rol
            var permisosActuales = await _workFlow.Repository<RolePermission>()
                                          .FindAsync(rp => rp.RoleId == roleId);

            if (permisosActuales.Any())
            {
                _workFlow.Repository<RolePermission>().RemoveRange(permisosActuales);
            }

            // 2. Insertamos los nuevos permisos si la lista no está vacía
            if (permissionIds != null && permissionIds.Any())
            {
                foreach (var pId in permissionIds)
                {
                    await _workFlow.Repository<RolePermission>().AddAsync(new RolePermission
                    {
                        RoleId = roleId,
                        PermissionId = pId
                    });
                }
            }

            // 3. Confirmamos los cambios en la base de datos
            await _workFlow.CompleteAsync();
            return (true, "Permisos actualizados con éxito");
        }
    }
}