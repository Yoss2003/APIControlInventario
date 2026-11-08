using InventoryAPI.Models;
using InventoryAPI.Data;
using InventoryAPI.Services;
using Npgsql;
using InventoryAPI.Services.IServices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryAPI.Controllers;

public sealed class BrandsController(IBrandService brandService, AppDbContext db) : BaseApiController
{
    private bool Developer => User.FindFirst("RoleId")?.Value == "1";
    private bool CanManageMaster => User.FindFirst("RoleId")?.Value is "1" or "2";
    private Task<Inventory?> Master() => db.Inventories
        .FirstOrDefaultAsync(i => i.CompanyId == CompanyId && i.IsMaster && i.IsActive);
    private int CompanyId => ObtenerEmpresaSegura();
    private int UserId => ObtenerUsuarioSeguro();
    private IQueryable<int> Available(bool write) => db.Inventories.Where(i => i.CompanyId == CompanyId &&
        (!i.IsMaster && (Developer || i.UserId == UserId || db.SharedInventories.Any(s => s.InventoryId == i.Id &&
        s.CompanyId == CompanyId && s.UserId == UserId &&
        (!write || s.AccessLevel == SharedInventory.AccessMode.Editor))))).Select(i => i.Id);

    [HttpGet]
    public async Task<IActionResult> GetBrands()
    {
        var allowed = await Available(false).ToListAsync();
        var brands = await brandService.GetAllByCompanyIdAsync(CompanyId);
        var master = await Master();
        return Ok(brands.Where(b => allowed.Contains(b.InventoryId) ||
            (master != null && b.InventoryId == master.Id)).ToArray());
    }
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetBrand(int id)
    {
        var b = await brandService.GetByIdAsync(id);
        if (b is null || b.CompanyId != CompanyId) return NotFound();
        var master = await Master();
        return b.InventoryId == master?.Id || await Available(false).ContainsAsync(b.InventoryId)
            ? Ok(b) : NotFound();
    }
    [HttpPost]
    public async Task<IActionResult> PostBrand([FromBody] Brand brand, [FromQuery] bool allowSimilar = false)
    {
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(brand.Name))
            return BadRequest(new { error = "Nombre de marca obligatorio." });
        var master = await Master();
        if (master is null) return Conflict(new { error = "La empresa no tiene inventario maestro activo." });
        if (brand.InventoryId != master.Id &&
            !await Available(true).ContainsAsync(brand.InventoryId)) return Forbid();
        if (brand.InventoryId == master.Id && !await Available(true).AnyAsync()) return Forbid();
        if (!await db.Categories.AnyAsync(c => c.Id == brand.CategoryId && c.InventoryId == master.Id &&
            c.CompanyId == CompanyId && c.IsActive))
            return BadRequest(new { error = "La categoría no pertenece al catálogo maestro." });

        brand.Id = 0;
        brand.CompanyId = CompanyId;
        brand.InventoryId = master.Id;
        brand.IsActive = true;
        brand.Name = brand.Name.Trim();
        brand.NameKey = CatalogNames.Key(brand.Name);
        if (brand.NameKey.Length == 0) return BadRequest(new { error = "Nombre inválido." });

        var candidates = await db.Brands.AsNoTracking()
            .Where(b => b.CompanyId == CompanyId && b.CategoryId == brand.CategoryId && b.IsActive)
            .Select(b => new { b.Id, b.Name, b.NameKey }).ToListAsync();
        var exact = candidates.FirstOrDefault(b => b.NameKey == brand.NameKey);
        if (exact != null)
        {
            var previous = await db.Brands.AsNoTracking().FirstOrDefaultAsync(b => b.Id == exact.Id);
            if (previous != null) { previous.Reused = true; return Ok(previous); }
        }
        var similar = candidates.Where(b => CatalogNames.Similar(b.Name, brand.Name))
            .Take(5).Select(b => new { b.Id, b.Name }).ToList();
        if (similar.Count > 0 && !allowSimilar)
            return Conflict(new { code = "similar_name", error = "Existe una marca de nombre similar.", matches = similar });

        try
        {
            db.Brands.Add(brand);
            await db.SaveChangesAsync();
            return CreatedAtAction(nameof(GetBrand), new { id = brand.Id }, brand);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg &&
            pg.SqlState == PostgresErrorCodes.UniqueViolation &&
            pg.ConstraintName == "IX_Brands_Company_Category_NameKey")
        {
            db.Entry(brand).State = EntityState.Detached;
            var existing = await db.Brands.AsNoTracking().FirstOrDefaultAsync(b =>
                b.CompanyId == CompanyId && b.CategoryId == brand.CategoryId &&
                b.NameKey == brand.NameKey && b.IsActive);
            if (existing is null) return Conflict(new { error = "Marca duplicada. Recarga el catálogo." });
            existing.Reused = true;
            return Ok(existing);
        }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> PutBrand(int id, [FromBody] Brand brand)
    {
        if (id != brand.Id || !ModelState.IsValid) return BadRequest();
        var existing = await brandService.GetByIdAsync(id);
        if (existing is null || existing.CompanyId != CompanyId) return NotFound();
        if (!CanManageMaster) return Forbid();
        var master = await Master();
        if (existing.InventoryId != master?.Id &&
            !await Available(true).ContainsAsync(existing.InventoryId)) return Forbid();
        if (string.IsNullOrWhiteSpace(brand.Name)) return BadRequest(new { error = "Nombre obligatorio." });
        existing.Name = brand.Name.Trim();
        existing.NameKey = CatalogNames.Key(existing.Name);
        if (await db.Brands.AnyAsync(b => b.Id != id && b.CompanyId == CompanyId &&
            b.CategoryId == existing.CategoryId && b.IsActive && b.NameKey == existing.NameKey))
            return Conflict(new { error = "Esta marca ya existe." });
        return await brandService.UpdateAsync(existing) ? NoContent() : BadRequest();
    }
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteBrand(int id)
    {
        var existing = await brandService.GetByIdAsync(id);
        if (existing is null || existing.CompanyId != CompanyId) return NotFound();
        if (!CanManageMaster) return Forbid();
        var master = await Master();
        if (existing.InventoryId != master?.Id &&
            !await Available(true).ContainsAsync(existing.InventoryId)) return Forbid();
        if (await db.Articles.AnyAsync(a => a.BrandId == id))
            return Conflict(new { error = "Esta marca está vinculada a artículos. No puede desactivarse." });
        existing.IsActive = false;
        return await brandService.UpdateAsync(existing) ? NoContent() : BadRequest();
    }
}
