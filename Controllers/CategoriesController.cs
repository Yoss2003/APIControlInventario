using InventoryAPI.Models;
using InventoryAPI.Data;
using InventoryAPI.Services.IServices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryAPI.Controllers;

public sealed class CategoriesController(ICategoryService categoryService, AppDbContext db) : BaseApiController
{
    private int CompanyId => ObtenerEmpresaSegura();
    private int UserId => ObtenerUsuarioSeguro();
    private bool Developer => User.FindFirst("RoleId")?.Value == "1";
    private bool CanManageMaster => User.FindFirst("RoleId")?.Value is "1" or "2";
    private Task<Inventory?> Master() => db.Inventories
        .FirstOrDefaultAsync(i => i.CompanyId == CompanyId && i.IsMaster && i.IsActive);
    private IQueryable<int> Allowed(bool edit) => db.Inventories.Where(i => i.CompanyId == CompanyId &&
        (!i.IsMaster && (Developer || i.UserId == UserId || db.SharedInventories.Any(sh =>
        sh.CompanyId == CompanyId && sh.InventoryId == i.Id && sh.UserId == UserId &&
        (!edit || sh.AccessLevel == SharedInventory.AccessMode.Editor))))).Select(i => i.Id);
    private async Task<bool> ParentValid(Category category) => category.ParentCategoryId is null ||
        await db.Categories.AnyAsync(c => c.Id == category.ParentCategoryId && c.CompanyId == CompanyId &&
            c.InventoryId == category.InventoryId && c.ParentCategoryId == null && c.IsActive);

    [HttpGet]
    public async Task<IActionResult> GetCategories()
    {
        var ids = await Allowed(false).ToListAsync();
        var items = await categoryService.GetAllByCompanyIdAsync(CompanyId);
        var master = await Master();
        return Ok(items.Where(c => ids.Contains(c.InventoryId) ||
            (master != null && c.InventoryId == master.Id)).ToArray());
    }
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetCategory(int id)
    {
        var item = await categoryService.GetByIdAsync(id);
        if (item is null || item.CompanyId != CompanyId) return NotFound();
        var master = await Master();
        return item.InventoryId == master?.Id || await Allowed(false).ContainsAsync(item.InventoryId)
            ? Ok(item) : NotFound();
    }
    [HttpPost]
    public async Task<IActionResult> PostCategory([FromBody] Category category)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var master = await Master();
        if (master is null) return Conflict(new { error = "La empresa no tiene inventario maestro activo." });
        // Un usuario puede registrar catálogos generales desde un inventario editable.
        // El formulario de subcategoría puede remitir directamente el ID del maestro.
        if (category.InventoryId != master.Id &&
            !await Allowed(true).ContainsAsync(category.InventoryId)) return Forbid();
        if (category.InventoryId == master.Id &&
            !await Allowed(true).AnyAsync()) return Forbid();
        category.InventoryId = master.Id;
        if (!await ParentValid(category)) return BadRequest(new { error = "El grupo padre debe estar activo y pertenecer al catálogo maestro." });
        category.Id = 0; category.CompanyId = CompanyId;
        var (success, message) = await categoryService.CreateCategoryAsync(category);
        return success ? CreatedAtAction(nameof(GetCategory), new { id = category.Id }, category)
            : BadRequest(new { error = message });
    }
    [HttpPut("{id:int}")]
    public async Task<IActionResult> PutCategory(int id, [FromBody] Category category)
    {
        if (id != category.Id || !ModelState.IsValid) return BadRequest(ModelState);
        var old = await categoryService.GetByIdAsync(id);
        if (old is null || old.CompanyId != CompanyId) return NotFound();
        if (!CanManageMaster) return Forbid();
        var master = await Master();
        if (old.InventoryId != master?.Id &&
            !await Allowed(true).ContainsAsync(old.InventoryId)) return Forbid();
        if (old.InventoryId != category.InventoryId) return Conflict(new { error = "No se puede mover una categoría de inventario." });
        if (!await ParentValid(category)) return BadRequest(new { error = "Grupo padre no válido para este inventario." });
        category.CompanyId = CompanyId;
        var (success, message) = await categoryService.UpdateCategoryAsync(id, category);
        return success ? NoContent() : BadRequest(new { error = message });
    }
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var item = await categoryService.GetByIdAsync(id);
        if (item is null || item.CompanyId != CompanyId) return NotFound();
        if (!CanManageMaster) return Forbid();
        var master = await Master();
        if (item.InventoryId != master?.Id &&
            !await Allowed(true).ContainsAsync(item.InventoryId)) return Forbid();
        if (await db.Articles.AnyAsync(a => a.CategoryId == id) ||
            await db.Categories.AnyAsync(c => c.ParentCategoryId == id))
            return Conflict(new { error = "La categoría tiene artículos o subcategorías vinculadas. No puede desactivarse." });
        var (success, message) = await categoryService.DeleteCategoryAsync(id, UserId.ToString());
        return success ? NoContent() : BadRequest(new { error = message });
    }
}
