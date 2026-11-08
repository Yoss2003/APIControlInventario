using InventoryAPI.Data;
using InventoryAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace InventoryAPI.Services;

/// <summary>
/// Crea inventarios base sin duplicarlos. No traslada artículos ni movimientos.
/// </summary>
public static class InventoryProvisioner
{
    public static Inventory NewMaster(int companyId)
    {
        if (companyId <= 0) throw new ArgumentOutOfRangeException(nameof(companyId));
        var now = DateTime.UtcNow;
        return new Inventory
        {
            CompanyId = companyId,
            UserId = 0,
            Username = "Sistema",
            InventoryName = $"MAESTRO_EMPRESA_{companyId}",
            Alias = "Inventario maestro",
            IsMaster = true,
            IsActive = true,
            CreationDate = now,
            ModificationDate = now
        };
    }

    public static Inventory NewPersonal(User user)
    {
        if (user.CompanyId is null or <= 0 || user.Id <= 0)
            throw new InvalidOperationException("No es posible crear el inventario sin usuario y empresa válidos.");
        var now = DateTime.UtcNow;
        return new Inventory
        {
            CompanyId = user.CompanyId.Value,
            UserId = user.Id,
            Username = user.Username ?? string.Empty,
            InventoryName = $"{user.Username}_Invent_{user.Id}",
            Alias = "Inventario principal",
            IsMaster = false,
            IsActive = true,
            CreationDate = now,
            ModificationDate = now
        };
    }

    /// <summary>
    /// Se ejecuta tras Database.Migrate(). Es necesario para empresas y usuarios
    /// preexistentes: no vuelve a crear el maestro ni un inventario personal activo.
    /// </summary>
    public static async Task EnsureExistingAsync(AppDbContext db)
    {
        var companyIds = await db.Companies.AsNoTracking().Select(x => x.Id).ToListAsync();
        foreach (var companyId in companyIds)
        {
            if (await db.Inventories.AnyAsync(i => i.CompanyId == companyId && i.IsMaster))
                continue;
            db.Inventories.Add(NewMaster(companyId));
            await db.SaveChangesAsync();
        }

        var users = await db.Users.AsNoTracking()
            .Where(u => u.CompanyId.HasValue)
            .Select(u => new User { Id = u.Id, CompanyId = u.CompanyId, Username = u.Username })
            .ToListAsync();

        foreach (var user in users)
        {
            if (await db.Inventories.AnyAsync(i =>
                i.CompanyId == user.CompanyId && i.UserId == user.Id && !i.IsMaster && i.IsActive))
                continue;

            db.Inventories.Add(NewPersonal(user));
            await db.SaveChangesAsync();
        }
    }
}
