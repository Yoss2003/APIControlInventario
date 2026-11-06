using ControlInventario.Shared.Models;
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
    private IQueryable<int> Allowed(bool edit) => db.Inventories.Where(i => i.CompanyId == CompanyId &&
        (Developer || i.UserId == UserId || db.SharedInventories.Any(sh =>
        sh.CompanyId == CompanyId && sh.InventoryId == i.Id && sh.UserId == UserId &&
        (!edit || sh.AccessLevel == SharedInventory.AccessMode.Editor)))).Select(i => i.Id);
    private async Task<bool> ParentValid(Category category) => category.ParentCategoryId is null ||
        await db.Categories.AnyAsync(c => c.Id == category.ParentCategoryId && c.CompanyId == CompanyId &&
            c.InventoryId == category.InventoryId && c.ParentCategoryId == null && c.IsActive);

    [HttpGet]
    public async Task<IActionResult> GetCategories()
    {
        var ids = await Allowed(false).ToListAsync();
        var items = await categoryService.GetAllByCompanyIdAsync(CompanyId);
        return Ok(items.Where(c => ids.Contains(c.InventoryId)).ToArray());
    }
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetCategory(int id)
    {
        var item = await categoryService.GetByIdAsync(id);
        return item is null || item.CompanyId != CompanyId || !await Allowed(false).ContainsAsync(item.InventoryId)
            ? NotFound() : Ok(item);
    }
    [HttpPost]
    public async Task<IActionResult> PostCategory([FromBody] Category category)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (!await Allowed(true).ContainsAsync(category.InventoryId)) return Forbid();
        if (!await ParentValid(category)) return BadRequest(new { error = "El grupo padre debe estar activo y pertenecer al mismo inventario." });
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
        if (!await Allowed(true).ContainsAsync(old.InventoryId)) return Forbid();
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
        if (!await Allowed(true).ContainsAsync(item.InventoryId)) return Forbid();
        if (await db.Articles.AnyAsync(a => a.CategoryId == id) ||
            await db.Categories.AnyAsync(c => c.ParentCategoryId == id))
            return Conflict(new { error = "La categoría tiene artículos o subcategorías vinculadas. No puede desactivarse." });
        var (success, message) = await categoryService.DeleteCategoryAsync(id, UserId.ToString());
        return success ? NoContent() : BadRequest(new { error = message });
    }
}
