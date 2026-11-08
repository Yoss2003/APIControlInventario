using InventoryAPI.Data;
using InventoryAPI.Models;
using InventoryAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryAPI.Controllers;

/// <summary>
/// Parámetros generales por empresa. Los secretos no se publican como catálogo.
/// </summary>
public sealed class ParametersController(AppDbContext db) : BaseApiController
{
    private int CompanyId => ObtenerEmpresaSegura();
    private int UserId => ObtenerUsuarioSeguro();
    private bool CanManage => User.FindFirst("RoleId")?.Value is "1" or "2";
    private bool IsDeveloper => User.FindFirst("RoleId")?.Value == "1";

    private static bool IsSecret(Parameters parameter)
    {
        var type = (parameter.ParameterType ?? "").ToUpperInvariant();
        var name = (parameter.Name ?? "").ToUpperInvariant();
        return new[] { "API_KEY", "TOKEN", "PASSWORD", "SMTP", "SECRET", "CREDENTIAL", "AUTH_KEY" }
            .Any(part => type.Contains(part, StringComparison.Ordinal) ||
                         name.Contains(part, StringComparison.Ordinal));
    }

    private Task<Inventory?> Master() => db.Inventories
        .FirstOrDefaultAsync(i => i.CompanyId == CompanyId && i.IsMaster && i.IsActive);

    // InventoryId=0 es el formato histórico de los parámetros de la Administración Central.
    // Conservamos esos IDs y valores; no trasladamos ni duplicamos los 26 registros existentes.
    private static bool IsCompanyParameter(Parameters parameter, int masterId) =>
        parameter.InventoryId == 0 || parameter.InventoryId == masterId;

    private Task<bool> CanCreateAsync(int inventoryId)
    {
        var developer = IsDeveloper;
        return db.Inventories.AnyAsync(i => i.CompanyId == CompanyId && !i.IsMaster && i.IsActive &&
            (i.Id == inventoryId || inventoryId == 0) &&
            (developer || i.UserId == UserId || db.SharedInventories.Any(sh =>
                sh.CompanyId == CompanyId && sh.InventoryId == i.Id && sh.UserId == UserId &&
                sh.AccessLevel == SharedInventory.AccessMode.Editor)));
    }

    // Acceso separado para la clave central; nunca aparece en GET de catálogos.
    // Si existe en InventoryId=0, se actualiza ese registro sin crear otro.
    [HttpPut("GlobalToken")]
    public async Task<IActionResult> UpdateGlobalToken([FromBody] string newToken)
    {
        if (CompanyId != 1 || !IsDeveloper) return Forbid();
        if (string.IsNullOrWhiteSpace(newToken)) return BadRequest(new { error = "Token vacío." });
        var master = await Master();
        if (master is null) return Conflict(new { error = "Inventario maestro no encontrado." });
        var item = await db.Parameters.FirstOrDefaultAsync(p =>
            p.CompanyId == 1 && p.ParameterType == "API_KEY" && p.Name == "Decolecta_Token");
        if (item is null)
        {
            db.Parameters.Add(new Parameters
            {
                CompanyId = 1, InventoryId = master.Id, ParameterType = "API_KEY",
                Name = "Decolecta_Token", Description = newToken
            });
        }
        else item.Description = newToken;
        await db.SaveChangesAsync();
        return Ok(new { message = "Token maestro actualizado." });
    }

    [HttpGet]
    public async Task<IActionResult> GetParameters()
    {
        var all = await db.Parameters.AsNoTracking().Where(p => p.CompanyId == CompanyId).ToListAsync();
        return Ok(all.Where(p => !IsSecret(p)).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetParameter(int id)
    {
        var item = await db.Parameters.AsNoTracking().FirstOrDefaultAsync(p =>
            p.Id == id && p.CompanyId == CompanyId);
        return item is null || IsSecret(item) ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> PostParameter([FromBody] Parameters input)
    {
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(input.Name) ||
            string.IsNullOrWhiteSpace(input.ParameterType))
            return BadRequest(ModelState);
        if (IsSecret(input)) return Forbid();

        var master = await Master();
        if (master is null) return Conflict(new { error = "Inventario maestro no encontrado." });
        if (input.InventoryId != master.Id && !await CanCreateAsync(input.InventoryId))
            return Forbid();
        if (input.InventoryId == master.Id && !await CanCreateAsync(0))
            return Forbid();

        await using var tx = await db.Database.BeginTransactionAsync();
        // Serializa las altas de parámetros dentro de la misma empresa.
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(20261107, {CompanyId})");
        var existing = await db.Parameters.AsNoTracking()
            .Where(p => p.CompanyId == CompanyId &&
                (p.InventoryId == 0 || p.InventoryId == master.Id)).ToListAsync();
        var original = existing.FirstOrDefault(p =>
            CatalogNames.Key(p.ParameterType) == CatalogNames.Key(input.ParameterType) &&
            CatalogNames.Key(p.Name) == CatalogNames.Key(input.Name));
        if (original is not null)
        {
            await tx.CommitAsync();
            return Ok(new { original.Id, original.InventoryId, original.CompanyId, original.ParameterType,
                original.Name, original.Description, reused = true });
        }

        input.Id = 0;
        input.CompanyId = CompanyId;
        input.InventoryId = master.Id;
        input.ParameterType = input.ParameterType.Trim();
        input.Name = input.Name.Trim();
        db.Parameters.Add(input);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return CreatedAtAction(nameof(GetParameter), new { id = input.Id }, input);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> PutParameter(int id, [FromBody] Parameters input)
    {
        if (!CanManage) return Forbid();
        if (id != input.Id || !ModelState.IsValid || IsSecret(input)) return BadRequest();
        var master = await Master();
        if (master is null) return Conflict(new { error = "Inventario maestro no encontrado." });
        var item = await db.Parameters.FirstOrDefaultAsync(p =>
            p.Id == id && p.CompanyId == CompanyId &&
            (p.InventoryId == 0 || p.InventoryId == master.Id));
        if (item is null) return NotFound();
        if (IsSecret(item)) return Forbid();
        var similar = await db.Parameters.AsNoTracking().Where(p => p.CompanyId == CompanyId &&
            (p.InventoryId == 0 || p.InventoryId == master.Id) && p.Id != id).ToListAsync();
        if (similar.Any(p => CatalogNames.Key(p.ParameterType) == CatalogNames.Key(input.ParameterType) &&
            CatalogNames.Key(p.Name) == CatalogNames.Key(input.Name)))
            return Conflict(new { error = "Ya existe ese parámetro en el maestro." });

        item.Name = input.Name.Trim();
        item.ParameterType = input.ParameterType.Trim();
        item.Description = input.Description;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteParameter(int id)
    {
        if (!CanManage) return Forbid();
        var master = await Master();
        if (master is null) return Conflict(new { error = "Inventario maestro no encontrado." });
        var item = await db.Parameters.FirstOrDefaultAsync(p =>
            p.Id == id && p.CompanyId == CompanyId &&
            (p.InventoryId == 0 || p.InventoryId == master.Id));
        if (item is null) return NotFound();
        if (IsSecret(item)) return Forbid();
        db.Parameters.Remove(item);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
