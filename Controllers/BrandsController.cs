using ControlInventario.Shared.Models;
using InventoryAPI.Data;
using InventoryAPI.Services.IServices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryAPI.Controllers;

public sealed class BrandsController(IBrandService brandService, AppDbContext db) : BaseApiController
{
    private bool Developer => User.FindFirst("RoleId")?.Value == "1";
    private int CompanyId => ObtenerEmpresaSegura();
    private int UserId => ObtenerUsuarioSeguro();
    private IQueryable<int> Available(bool write) => db.Inventories.Where(i => i.CompanyId == CompanyId &&
        (Developer || i.UserId == UserId || db.SharedInventories.Any(s => s.InventoryId == i.Id &&
        s.CompanyId == CompanyId && s.UserId == UserId &&
        (!write || s.AccessLevel == SharedInventory.AccessMode.Editor)))).Select(i => i.Id);

    [HttpGet]
    public async Task<IActionResult> GetBrands()
    {
        var allowed = await Available(false).ToListAsync();
        var brands = await brandService.GetAllByCompanyIdAsync(CompanyId);
        return Ok(brands.Where(b => allowed.Contains(b.InventoryId)).ToArray());
    }
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetBrand(int id)
    {
        var b = await brandService.GetByIdAsync(id);
        return b is null || b.CompanyId != CompanyId || !await Available(false).ContainsAsync(b.InventoryId)
            ? NotFound() : Ok(b);
    }
    [HttpPost]
    public async Task<IActionResult> PostBrand([FromBody] Brand brand)
    {
        if (!ModelState.IsValid || !await Available(true).ContainsAsync(brand.InventoryId)) return Forbid();
        if (!await db.Categories.AnyAsync(c => c.Id == brand.CategoryId && c.InventoryId == brand.InventoryId && c.CompanyId == CompanyId))
            return BadRequest(new { error = "La categoría no corresponde al inventario." });
        brand.Id = 0; brand.CompanyId = CompanyId;
        if (!await brandService.CreateAsync(brand)) return BadRequest(new { error = "No se pudo guardar la marca." });
        return CreatedAtAction(nameof(GetBrand), new { id = brand.Id }, brand);
    }
    [HttpPut("{id:int}")]
    public async Task<IActionResult> PutBrand(int id, [FromBody] Brand brand)
    {
        if (id != brand.Id || !ModelState.IsValid) return BadRequest();
        var existing = await brandService.GetByIdAsync(id);
        if (existing is null || existing.CompanyId != CompanyId) return NotFound();
        if (!await Available(true).ContainsAsync(existing.InventoryId)) return Forbid();
        if (string.IsNullOrWhiteSpace(brand.Name)) return BadRequest(new { error = "Nombre obligatorio." });
        existing.Name = brand.Name.Trim();
        return await brandService.UpdateAsync(existing) ? NoContent() : BadRequest();
    }
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteBrand(int id)
    {
        var existing = await brandService.GetByIdAsync(id);
        if (existing is null || existing.CompanyId != CompanyId) return NotFound();
        if (!await Available(true).ContainsAsync(existing.InventoryId)) return Forbid();
        if (await db.Articles.AnyAsync(a => a.BrandId == id))
            return Conflict(new { error = "Esta marca está vinculada a artículos. No puede desactivarse." });
        existing.IsActive = false;
        return await brandService.UpdateAsync(existing) ? NoContent() : BadRequest();
    }
}
