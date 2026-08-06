using ControlInventario.Shared.Models;
using InventoryAPI.Models.DTO;
using InventoryAPI.Repositories;
using InventoryAPI.Repositories.IRepositories;
using InventoryAPI.Services.IServices;

namespace InventoryAPI.Services
{
    public class InventoryService(IWorkFlow workFlow) : WorkContainer<Inventory>(workFlow), IInventoryService
    {
        public async Task<(bool Success, string Message)> ShareInventoryAsync(ShareRequestDTO request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.GuestIdentifier))
            {
                return (false, "Datos de invitación inválidos.");
            }

            if (!Enum.IsDefined(request.AccessLevel.GetType(), request.AccessLevel))
            {
                return (false, "El nivel de acceso enviado no es válido (Debe ser 1 para Lector o 2 para Editor).");
            }

            // Buscamos al usuario por Username o Email usando tu Repositorio Genérico
            var matchUsers = await _workFlow.Repository<User>().FindAsync(u =>
                u.Username!.ToLower() == request.GuestIdentifier.Trim().ToLower() ||
                u.Email!.ToLower() == request.GuestIdentifier.Trim().ToLower());
            var guestUser = matchUsers.FirstOrDefault();

            if (guestUser == null)
            {
                return (false, "El usuario o correo ingresado no existe en el sistema.");
            }

            var inventory = await _workFlow.Repository<Inventory>().GetByIdAsync(request.InventoryId);
            if (inventory == null)
            {
                return (false, "El inventario especificado no existe.");
            }

            if (inventory.UserId == guestUser.Id)
            {
                return (false, "No puedes compartir el inventario con el dueño del mismo.");
            }

            var matchShares = await _workFlow.Repository<SharedInventory>().FindAsync(s =>
                s.InventoryId == request.InventoryId && s.UserId == guestUser.Id);

            if (matchShares.Any())
            {
                return (false, "Este inventario ya se encuentra compartido con este usuario.");
            }

            var sharedRelation = new SharedInventory
            {
                InventoryId = request.InventoryId,
                UserId = guestUser.Id,
                AccessLevel = request.AccessLevel,
                SharedDate = DateTime.Now
            };

            try
            {
                await _workFlow.Repository<SharedInventory>().AddAsync(sharedRelation);
                await _workFlow.CompleteAsync();

                return (true, $"Inventario compartido con {guestUser.Username} en modo [{request.AccessLevel}] con éxito.");
            }
            catch (Exception ex)
            {
                return (false, $"Error interno al procesar la compartición: {ex.Message}");
            }
        }
    }
}