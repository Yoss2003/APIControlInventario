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
        // El administrador no hereda acceso al stock privado de sus empleados.
        // Solo ve sus inventarios propios o compartidos; el desarrollador
        // puede inspeccionar operativos del contexto empresarial elegido.
        var manageAll = IsDeveloper;
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
    /// Supervisión administrativa de inventarios operativos de la empresa.
    /// Solo metadatos; no concede acceso al stock si no existe asignación.
    /// </summary>
    [HttpGet("company-operational")]
    public async Task<IActionResult> CompanyOperational()
    {
        if (!TryScope(out var company, out _)) return Unauthorized();
        if (!await permissions.HasAsync(User, InventoryPermissionCodes.ManageInventoryAccess))
            return Forbid();
        return Ok(await _db.Inventories.AsNoTracking()
            .Where(i => i.CompanyId == company && !i.IsMaster)
            .OrderBy(i => i.Alias ?? i.InventoryName).ToListAsync());
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
        if (!IsDeveloper) return Forbid();
        var masters = await _db.Inventories.AsNoTracking()
            .Where(i => i.IsMaster && (IsDeveloper || i.CompanyId == company))
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

    /// <summary>
    /// Vista de solo lectura del catálogo de un maestro. Para ver otra empresa
    /// se requiere rol desarrollador; no modifica el contexto ni habilita stock.
    /// </summary>
    [HttpGet("masters/{masterId:int}/catalogs")]
    public async Task<IActionResult> GetMasterCatalogs(int masterId)
    {
        if (!TryScope(out var companyId, out _)) return Unauthorized();
        if (!IsDeveloper) return Forbid();
        var master = await _db.Inventories.AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == masterId && i.IsMaster &&
                (IsDeveloper || i.CompanyId == companyId));
        if (master is null) return NotFound();

        var categories = await _db.Categories.AsNoTracking()
            .Where(c => c.CompanyId == master.CompanyId && c.InventoryId == masterId && c.IsActive)
            .OrderBy(c => c.Name).Select(c => new { c.Id, c.Name, c.ParentCategoryId })
            .ToListAsync();
        var brands = await _db.Brands.AsNoTracking()
            .Where(b => b.CompanyId == master.CompanyId && b.InventoryId == masterId && b.IsActive)
            .OrderBy(b => b.Name).Select(b => new { b.Id, b.Name, b.CategoryId })
            .ToListAsync();
        var suppliers = await _db.Suppliers.AsNoTracking()
            .Where(p => p.CompanyId == master.CompanyId && p.InventoryId == masterId && p.IsActive)
            .OrderBy(p => p.BusinessName)
            .Select(p => new { p.Id, p.Ruc, p.BusinessName, p.ContactName, p.Email, p.Phone })
            .ToListAsync();
        return Ok(new
        {
            master.Id, master.CompanyId, master.InventoryName,
            categories, brands, suppliers
        });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetInventory(int id)
    {
        if (!TryScope(out var company, out var user)) return Unauthorized();
        var viewMaster = await permissions.HasAsync(User, InventoryPermissionCodes.ViewMaster);
        var manager = IsDeveloper;
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
        // El desarrollador que navega otra empresa debe elegir expresamente
        // un propietario de esa empresa; nunca se apropia de stock ajeno.
        if (!await _db.Users.AnyAsync(u => u.Id == user && u.CompanyId == company && u.IsActive))
            return Conflict(new { error = "Selecciona un propietario de esta empresa mediante el alta administrada de inventario." });
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

    public sealed record AssignedInventoryInput(
        int OwnerUserId, string InventoryName, string? Alias);

    /// <summary>Creación explícita por administrador en un usuario de su empresa.</summary>
    [HttpPost("assign")]
    public async Task<IActionResult> AssignInventory([FromBody] AssignedInventoryInput request)
    {
        if (!TryScope(out var company, out _)) return Unauthorized();
        if (!IsDeveloper && !IsAdmin) return Forbid();
        if (!await permissions.HasAsync(User, InventoryPermissionCodes.CreateInventory) ||
            !await permissions.HasAsync(User, InventoryPermissionCodes.ManageInventoryAccess))
            return Forbid();
        if (request.OwnerUserId <= 0 || string.IsNullOrWhiteSpace(request.InventoryName) ||
            request.InventoryName.Trim().Length > 255)
            return BadRequest(new { error = "Indica un propietario y nombre válidos." });
        var owner = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u =>
            u.Id == request.OwnerUserId && u.CompanyId == company && u.IsActive && u.StatusId == 1);
        if (owner is null) return NotFound(new { error = "El propietario no existe o no pertenece a la empresa." });
        var now = DateTime.UtcNow;
        var item = new Inventory
        {
            CompanyId = company, UserId = owner.Id, Username = owner.Username ?? string.Empty,
            InventoryName = request.InventoryName.Trim(), Alias = request.Alias?.Trim(),
            CreationDate = now, ModificationDate = now, IsActive = true, IsMaster = false
        };
        _db.Inventories.Add(item);
        await _db.SaveChangesAsync();
        return Ok(new { item.Id, item.CompanyId, item.UserId, item.InventoryName, item.Alias });
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
