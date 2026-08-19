using ControlInventario.Shared.Models;
using ControlInventario.Shared.Models.DTO;
using InventoryAPI.Repositories;
using InventoryAPI.Repositories.IRepositories;
using InventoryAPI.Services.IServices;
using System.Diagnostics;

namespace InventoryAPI.Services
{
    public class InventoryService(IWorkFlow workFlow) : WorkContainer<Inventory>(workFlow), IInventoryService
    {
        public async Task<(bool Success, string Message)> ShareInventoryAsync(ShareRequestDTO request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.GuestIdentifier))
                {
                    return (false, "El identificador del invitado es inválido.");
                }

                // 1. CORRECCIÓN AQUÍ: Usamos FindAsync y luego FirstOrDefault
                var guestUsers = await _workFlow.Repository<User>().FindAsync(u =>
                    u.Username == request.GuestIdentifier ||
                    u.Email == request.GuestIdentifier ||
                    (u.Employee != null && u.Employee.DNI == request.GuestIdentifier));

                var guestUser = guestUsers.FirstOrDefault();

                if (guestUser == null)
                {
                    return (false, "El colaborador indicado no existe en el sistema.");
                }

                var existingShares = await _workFlow.Repository<SharedInventory>().FindAsync(s =>
                    s.InventoryId == request.InventoryId && s.UserId == guestUser.Id);

                var existingShare = existingShares.FirstOrDefault();

                if (existingShare != null)
                {
                    return (false, "El inventario ya se encuentra compartido con este colaborador.");
                }

                var newShare = new SharedInventory
                {
                    InventoryId = request.InventoryId,
                    UserId = guestUser.Id,
                    AccessLevel = request.AccessLevel,
                    SharedDate = DateTime.Now
                };

                await _workFlow.Repository<SharedInventory>().AddAsync(newShare);
                await _workFlow.CompleteAsync();

                return (true, "Inventario compartido exitosamente.");
            }
            catch (Exception ex)
            {
                return (false, $"Error interno del servidor: {ex.Message}");
            }
        }

        public async Task<IEnumerable<SharedInventory>> GetSharedInventoriesAsync(int inventoryId)
        {
            try
            {
                var allShared = await _workFlow.Repository<SharedInventory>()
                    .GetAllWithIncludeAsync(s => s.User!, s => s.User!.Employee!);

                var sharedRecords = allShared.Where(s => s.InventoryId == inventoryId).ToList();

                return sharedRecords;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error al listar compartidos: {ex.Message}");
                return new List<SharedInventory>();
            }
        }

        public async Task<bool> RevokeAccessAsync(int sharedInventoryId)
        {
            try
            {
                // 3. CORRECCIÓN AQUÍ: Usamos FindAsync y luego FirstOrDefault
                var sharedRecords = await _workFlow.Repository<SharedInventory>()
                                                  .FindAsync(s => s.Id == sharedInventoryId);

                var sharedRecord = sharedRecords.FirstOrDefault();

                if (sharedRecord == null)
                {
                    return false;
                }

                _workFlow.Repository<SharedInventory>().Delete(sharedRecord);
                await _workFlow.CompleteAsync();

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error al revocar acceso: {ex.Message}");
                return false;
            }
        }
    }
}