using InventoryAPI.Models;
using InventoryAPI.Models.DTO;
using InventoryAPI.Data;
using InventoryAPI.Services.IServices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryAPI.Controllers;

public class InventoriesController(IInventoryService inventoryService, AppDbContext db) : BaseApiController
{
    private bool IsDeveloper => User.FindFirst("RoleId")?.Value == "1";
    private bool CanManageMaster => User.FindFirst("RoleId")?.Value is "1" or "2";
    private readonly IInventoryService _service = inventoryService;
    private readonly AppDbContext _db = db;

    private bool TryScope(out int companyId, out int userId)
    {
        userId = ObtenerUsuarioSeguro();
        return int.TryParse(User.FindFirst("CompanyId")?.Value, out companyId) &&
            userId > 0 && companyId > 0;
    }
    private IQueryable<Inventory> Readable(int companyId, int userId)
    {
        bool canManageMaster = CanManageMaster;
        bool isDeveloper = IsDeveloper;
        return _db.Inventories.AsNoTracking().Where(i => i.CompanyId == companyId &&
            ((i.IsMaster && canManageMaster) ||
             (!i.IsMaster && (isDeveloper || i.UserId == userId ||
                _db.SharedInventories.Any(s => s.InventoryId == i.Id &&
                    s.UserId == userId && s.CompanyId == companyId)))));
    }

    [HttpGet]
    public async Task<IActionResult> GetInventories()
    {
        if (!TryScope(out var company, out var user)) return Unauthorized();
        return Ok(await Readable(company, user).OrderBy(i => i.Alias ?? i.InventoryName).ToListAsync());
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetInventory(int id)
    {
        if (!TryScope(out var company, out var user)) return Unauthorized();
        var item = await Readable(company, user).FirstOrDefaultAsync(i => i.Id == id);
        return item == null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> PostInventory([FromBody] Inventory inventory)
    {
        if (!TryScope(out var company, out var user)) return Unauthorized();
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(inventory.InventoryName)) return BadRequest(ModelState);
        // La ruta pública nunca permite crear otro inventario maestro.
        inventory.Id = 0; inventory.CompanyId = company; inventory.UserId = user; inventory.IsMaster = false;
        inventory.CreationDate = DateTime.UtcNow; inventory.ModificationDate = DateTime.UtcNow;
        inventory.IsActive = true;
        inventory.Username = await _db.Users.Where(u => u.Id == user).Select(u => u.Username).FirstOrDefaultAsync() ?? string.Empty;
        _db.Inventories.Add(inventory);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetInventory), new { id = inventory.Id }, inventory);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> PutInventory(int id, [FromBody] Inventory update)
    {
        if (!TryScope(out var company, out var user)) return Unauthorized();
        if (id != update.Id || !ModelState.IsValid) return BadRequest();
        var inventory = await _db.Inventories.FirstOrDefaultAsync(i => i.Id == id && i.CompanyId == company && (i.UserId == user || IsDeveloper));
        if (inventory == null) return NotFound();
        if (inventory.IsMaster) return Conflict(new { error = "El inventario maestro es administrado exclusivamente por el sistema." });
        if (string.IsNullOrWhiteSpace(update.InventoryName)) return BadRequest(new { error = "El nombre del inventario es obligatorio." });
        inventory.InventoryName = update.InventoryName.Trim(); inventory.Alias = update.Alias?.Trim();
        inventory.ModificationDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteInventory(int id)
    {
        if (!TryScope(out var company, out var user)) return Unauthorized();
        var inventory = await _db.Inventories.FirstOrDefaultAsync(i => i.Id == id && i.CompanyId == company && (i.UserId == user || IsDeveloper));
        if (inventory == null) return NotFound();
        if (inventory.IsMaster) return Conflict(new { error = "El inventario maestro es administrado exclusivamente por el sistema." });
        if (await _db.Articles.AnyAsync(a => a.InventoryId == id) ||
            await _db.Categories.AnyAsync(c => c.InventoryId == id) ||
            await _db.Brands.AnyAsync(b => b.InventoryId == id) ||
            await _db.SharedInventories.AnyAsync(sh => sh.InventoryId == id) ||
            await _db.ArticleAssignments.AnyAsync(a => a.InventoryId == id))
            return Conflict(new { error = "Inventario referenciado por artículos, categorías, marcas, asignaciones o permisos. No se puede desactivar." });
        inventory.IsActive = false; inventory.ModificationDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("Share")]
    public async Task<IActionResult> ShareInventory([FromBody] ShareRequestDTO request)
    {
        if (!TryScope(out var company, out var user)) return Unauthorized();
        if (!await _db.Inventories.AnyAsync(i => i.Id == request.InventoryId && i.CompanyId == company && !i.IsMaster && (i.UserId == user || IsDeveloper)))
            return Forbid();
        var result = await _service.ShareInventoryAsync(request);
        return result.Success ? Ok(new { message = result.Message }) : BadRequest(new { error = result.Message });
    }

    [HttpGet("{inventoryId:int}/Shared")]
    public async Task<IActionResult> GetSharedInventories(int inventoryId)
    {
        if (!TryScope(out var company, out var user)) return Unauthorized();
        if (!await _db.Inventories.AnyAsync(i => i.Id == inventoryId && i.CompanyId == company && !i.IsMaster && (i.UserId == user || IsDeveloper)))
            return Forbid();
        var records = await _service.GetSharedInventoriesAsync(inventoryId);
        return Ok(records.Select(x => new { x.Id, x.InventoryId, x.UserId,
            username = x.User?.Username, x.AccessLevel, x.SharedDate }));
    }

    [HttpDelete("Revoke/{sharedInventoryId:int}")]
    public async Task<IActionResult> RevokeAccess(int sharedInventoryId)
    {
        if (!TryScope(out var company, out var user)) return Unauthorized();
        var valid = await _db.SharedInventories.AnyAsync(s => s.Id == sharedInventoryId && s.CompanyId == company &&
            _db.Inventories.Any(i => i.Id == s.InventoryId && i.CompanyId == company && !i.IsMaster && (i.UserId == user || IsDeveloper)));
        if (!valid) return Forbid();
        return await _service.RevokeAccessAsync(sharedInventoryId)
            ? NoContent() : Conflict(new { error = "No se pudo revocar el permiso." });
    }
}
