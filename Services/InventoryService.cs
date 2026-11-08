using InventoryAPI.Models;
using InventoryAPI.Models.DTO;
using InventoryAPI.Data;
using InventoryAPI.Repositories;
using InventoryAPI.Repositories.IRepositories;
using InventoryAPI.Services.IServices;
using Microsoft.EntityFrameworkCore;

namespace InventoryAPI.Services;

public class InventoryService(IWorkFlow workFlow, AppDbContext db)
    : WorkContainer<Inventory>(workFlow), IInventoryService
{
    public async Task<(bool Success, string Message)> ShareInventoryAsync(ShareRequestDTO request)
    {
        if (request.InventoryId <= 0 || string.IsNullOrWhiteSpace(request.GuestIdentifier) ||
            request.AccessLevel is not (SharedInventory.AccessMode.Lector or SharedInventory.AccessMode.Editor))
            return (false, "Selecciona un inventario, destinatario y permiso válidos.");

        var source = await db.Inventories.AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == request.InventoryId && i.IsActive);
        if (source == null || source.IsMaster)
            return (false, "El inventario no existe, está desactivado o es un maestro no compartible.");

        string identifier = request.GuestIdentifier.Trim();
        var recipient = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.IsActive && u.CompanyId == source.CompanyId &&
                (u.Username == identifier || u.Email == identifier ||
                 (u.Employee != null && u.Employee.DNI == identifier)));
        if (recipient == null) return (false, "Destinatario no encontrado en la misma empresa.");
        if (source.UserId == recipient.Id) return (false, "No puedes compartir tu inventario contigo mismo.");
        if (await db.SharedInventories.AnyAsync(s => s.CompanyId == source.CompanyId &&
                s.InventoryId == source.Id && s.UserId == recipient.Id))
            return (false, "Este usuario ya tiene acceso al inventario.");

        db.SharedInventories.Add(new SharedInventory
        {
            CompanyId = source.CompanyId, InventoryId = source.Id, UserId = recipient.Id,
            AccessLevel = request.AccessLevel, SharedDate = DateTime.UtcNow
        });
        try
        {
            await db.SaveChangesAsync();
            return (true, "Inventario compartido con el usuario seleccionado.");
        }
        catch (DbUpdateException)
        {
            return (false, "No se pudo compartir: comprueba que el usuario no tenga ya acceso.");
        }
    }

    public async Task<IEnumerable<SharedInventory>> GetSharedInventoriesAsync(int inventoryId)
    {
        return await db.SharedInventories.AsNoTracking().Include(s => s.User)
            .Where(s => s.InventoryId == inventoryId)
            .OrderBy(s => s.UserId).ToListAsync();
    }

    public async Task<bool> RevokeAccessAsync(int sharedInventoryId)
    {
        var record = await db.SharedInventories.FindAsync(sharedInventoryId);
        if (record == null) return false;
        db.SharedInventories.Remove(record);
        return await db.SaveChangesAsync() > 0;
    }
}
