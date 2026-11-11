using InventoryAPI.Models;
using InventoryAPI.Models.DTO;
using InventoryAPI.Data;
using InventoryAPI.Services.IServices;
using InventoryAPI.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryAPI.Controllers;

public sealed class InventoriesController(
    IInventoryService inventoryService,
    AppDbContext db,
    InventoryAccessPolicy permissions) : BaseApiController
{
    private bool IsDeveloper => InventoryAccessPolicy.IsDeveloper(User);
    private bool IsAdmin => InventoryAccessPolicy.IsAdmin(User);
    private readonly IInventoryService _service = inventoryService;
    private readonly AppDbContext _db = db;

    private bool TryScope(out int companyId, out int userId)
    {
        companyId = InventoryAccessPolicy.CompanyId(User);
        userId = InventoryAccessPolicy.UserId(User);
        return companyId > 0 && userId > 0;
    }

    /// <summary>
    /// Inventarios operativos de la empresa del JWT y, si corresponde,
    /// su maestro. Los maestros nunca se incluyen en un selector de stock.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetInventories()
    {
        if (!TryScope(out var company, out var user)) return Unauthorized();
        var viewMaster = await permissions.HasAsync(User, InventoryPermissionCodes.ViewMaster);
        var manageAll = await permissions.HasAsync(User, InventoryPermissionCodes.ManageInventoryAccess) &&
                        (IsDeveloper || IsAdmin);
        var items = await _db.Inventories.AsNoTracking()
            .Where(i => i.CompanyId == company && i.IsActive &&
                (i.IsMaster ? viewMaster :
                    (manageAll || i.UserId == user ||
                     _db.SharedInventories.Any(s => s.InventoryId == i.Id &&
                         s.UserId == user && s.CompanyId == company))))
            .OrderBy(i => i.Alias ?? i.InventoryName).ToListAsync();
        return Ok(items);
    }

    /// <summary>
    /// El desarrollador puede consultar todos los maestros; cada otro rol
    /// únicamente el maestro de la empresa autenticada, si tiene permiso.
    /// No concede acceso al stock de las otras empresas.
    /// </summary>
    [HttpGet("masters")]
    public async Task<IActionResult> GetMasterInventories()
    {
        if (!TryScope(out var company, out _)) return Unauthorized();
        if (!await permissions.HasAsync(User, InventoryPermissionCodes.ViewMaster)) return Forbid();
        var masters = await _db.Inventories.AsNoTracking()
            .Where(i => i.IsMaster && i.IsActive && (IsDeveloper || i.CompanyId == company))
            .Join(_db.Companies.AsNoTracking(),
                i => i.CompanyId, c => c.Id,
                (i, c) => new
                {
                    i.Id, i.CompanyId, companyName = c.BusinessName,
                    i.InventoryName, i.Alias, i.IsMaster, i.IsActive
                })
            .OrderBy(i => i.companyName).ToListAsync();
        return Ok(masters);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetInventory(int id)
    {
        if (!TryScope(out var company, out var user)) return Unauthorized();
        var viewMaster = await permissions.HasAsync(User, InventoryPermissionCodes.ViewMaster);
        var manager = IsDeveloper || IsAdmin;
        var item = await _db.Inventories.AsNoTracking().FirstOrDefaultAsync(i =>
            i.Id == id && i.CompanyId == company && i.IsActive &&
            (i.IsMaster ? viewMaster :
                (manager || i.UserId == user ||
                 _db.SharedInventories.Any(s => s.InventoryId == i.Id &&
                    s.UserId == user && s.CompanyId == company))));
        return item == null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> PostInventory([FromBody] Inventory inventory)
    {
        if (!TryScope(out var company, out var user)) return Unauthorized();
        if (!await permissions.HasAsync(User, InventoryPermissionCodes.CreateInventory)) return Forbid();
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(inventory.InventoryName))
            return BadRequest(new { error = "El nombre del inventario es obligatorio." });

        inventory.Id = 0;
        inventory.CompanyId = company;
        inventory.UserId = user;
        inventory.IsMaster = false;
        inventory.CreationDate = DateTime.UtcNow;
        inventory.ModificationDate = DateTime.UtcNow;
        inventory.IsActive = true;
        inventory.InventoryName = inventory.InventoryName.Trim();
        inventory.Alias = inventory.Alias?.Trim();
        inventory.Username = await _db.Users.Where(u => u.Id == user)
            .Select(u => u.Username).FirstOrDefaultAsync() ?? string.Empty;
        _db.Inventories.Add(inventory);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetInventory), new { id = inventory.Id }, inventory);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> PutInventory(int id, [FromBody] Inventory update)
    {
        if (!TryScope(out var company, out var user)) return Unauthorized();
        if (!await permissions.HasAsync(User, InventoryPermissionCodes.CreateInventory)) return Forbid();
        if (id != update.Id || !ModelState.IsValid) return BadRequest();
        var manager = IsDeveloper || IsAdmin;
        var inventory = await _db.Inventories.FirstOrDefaultAsync(i => i.Id == id &&
            i.CompanyId == company && (i.UserId == user || manager));
        if (inventory == null) return NotFound();
        if (inventory.IsMaster) return Conflict(new { error = "El inventario maestro es administrado exclusivamente por el sistema." });
        if (!inventory.IsActive) return Conflict(new { error = "Inventario inactivo." });
        if (string.IsNullOrWhiteSpace(update.InventoryName))
            return BadRequest(new { error = "El nombre del inventario es obligatorio." });
        inventory.InventoryName = update.InventoryName.Trim();
        inventory.Alias = update.Alias?.Trim();
        inventory.ModificationDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteInventory(int id)
    {
        if (!TryScope(out var company, out var user)) return Unauthorized();
        if (!await permissions.HasAsync(User, InventoryPermissionCodes.CreateInventory)) return Forbid();
        var manager = IsDeveloper || IsAdmin;
        var inventory = await _db.Inventories.FirstOrDefaultAsync(i => i.Id == id &&
            i.CompanyId == company && (i.UserId == user || manager));
        if (inventory == null) return NotFound();
        if (inventory.IsMaster) return Conflict(new { error = "El inventario maestro es administrado exclusivamente por el sistema." });
        if (await _db.Articles.AnyAsync(a => a.InventoryId == id) ||
            await _db.Categories.AnyAsync(c => c.InventoryId == id) ||
            await _db.Brands.AnyAsync(b => b.InventoryId == id) ||
            await _db.SharedInventories.AnyAsync(sh => sh.InventoryId == id) ||
            await _db.ArticleAssignments.AnyAsync(a => a.InventoryId == id))
            return Conflict(new { error = "Inventario referenciado por artículos, categorías, marcas, asignaciones o permisos. No se puede desactivar." });
        inventory.IsActive = false;
        inventory.ModificationDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("Share")]
    public async Task<IActionResult> ShareInventory([FromBody] ShareRequestDTO request)
    {
        if (!TryScope(out var company, out var user)) return Unauthorized();
        if (!await permissions.HasAsync(User, InventoryPermissionCodes.ManageInventoryAccess)) return Forbid();
        var manager = IsDeveloper || IsAdmin;
        if (!await _db.Inventories.AnyAsync(i => i.Id == request.InventoryId &&
            i.CompanyId == company && i.IsActive && !i.IsMaster &&
            (i.UserId == user || manager))) return Forbid();
        var result = await _service.ShareInventoryAsync(request);
        return result.Success ? Ok(new { message = result.Message })
            : BadRequest(new { error = result.Message });
    }

    [HttpGet("{inventoryId:int}/Shared")]
    public async Task<IActionResult> GetSharedInventories(int inventoryId)
    {
        if (!TryScope(out var company, out var user)) return Unauthorized();
        if (!await permissions.HasAsync(User, InventoryPermissionCodes.ManageInventoryAccess)) return Forbid();
        var manager = IsDeveloper || IsAdmin;
        if (!await _db.Inventories.AnyAsync(i => i.Id == inventoryId &&
            i.CompanyId == company && i.IsActive && !i.IsMaster &&
            (i.UserId == user || manager))) return Forbid();
        var records = await _service.GetSharedInventoriesAsync(inventoryId);
        return Ok(records.Select(x => new { x.Id, x.InventoryId, x.UserId,
            username = x.User?.Username, x.AccessLevel, x.SharedDate }));
    }

    [HttpDelete("Revoke/{sharedInventoryId:int}")]
    public async Task<IActionResult> RevokeAccess(int sharedInventoryId)
    {
        if (!TryScope(out var company, out var user)) return Unauthorized();
        if (!await permissions.HasAsync(User, InventoryPermissionCodes.ManageInventoryAccess)) return Forbid();
        var manager = IsDeveloper || IsAdmin;
        var valid = await _db.SharedInventories.AnyAsync(s => s.Id == sharedInventoryId &&
            s.CompanyId == company &&
            _db.Inventories.Any(i => i.Id == s.InventoryId &&
                i.CompanyId == company && i.IsActive && !i.IsMaster &&
                (i.UserId == user || manager)));
        if (!valid) return Forbid();
        return await _service.RevokeAccessAsync(sharedInventoryId)
            ? NoContent() : Conflict(new { error = "No se pudo revocar el permiso." });
    }
}
