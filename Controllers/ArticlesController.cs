using InventoryAPI.Models;
using InventoryAPI.Data;
using InventoryAPI.Services.IServices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryAPI.Controllers;

public class ArticlesController(IArticleService articleService, AppDbContext db) : BaseApiController
{
    private bool IsDeveloper => User.FindFirst("RoleId")?.Value == "1";
    private readonly IArticleService _service = articleService;
    private readonly AppDbContext _db = db;
    private bool TryCompany(out int companyId) =>
        int.TryParse(User.FindFirst("CompanyId")?.Value, out companyId) && companyId > 0;

    // No confundir custodia con permiso de lectura de un almacén.
    private IQueryable<int> AccessibleInventories(int companyId, int userId, bool edit = false) =>
        _db.Inventories.Where(i => i.CompanyId == companyId && !i.IsMaster &&
            (IsDeveloper || i.UserId == userId || _db.SharedInventories.Any(share =>
                share.InventoryId == i.Id && share.UserId == userId && share.CompanyId == companyId &&
                (!edit || share.AccessLevel == SharedInventory.AccessMode.Editor))))
            .Select(i => i.Id);

    [HttpGet]
    public async Task<IActionResult> GetArticles()
    {
        if (!TryCompany(out var companyId)) return Unauthorized();
        var permitted = AccessibleInventories(companyId, ObtenerUsuarioSeguro());
        return Ok(await _db.Articles.AsNoTracking().Where(a => a.CompanyId == companyId && permitted.Contains(a.InventoryId))
            .Include(a => a.Details).OrderBy(a => a.Name).ToListAsync());
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetArticle(int id)
    {
        if (!TryCompany(out var companyId)) return Unauthorized();
        var permitted = AccessibleInventories(companyId, ObtenerUsuarioSeguro());
        var article = await _db.Articles.AsNoTracking().Include(a => a.Details)
            .FirstOrDefaultAsync(a => a.Id == id && a.CompanyId == companyId && permitted.Contains(a.InventoryId));
        return article is null ? NotFound() : Ok(article);
    }

    private async Task<string?> ValidateMasterData(Article article, int companyId)
    {
        if (!await AccessibleInventories(companyId, ObtenerUsuarioSeguro(), true).ContainsAsync(article.InventoryId))
            return "No tienes permiso para modificar este almacén.";
        if (!await _db.Categories.AnyAsync(c => c.Id == article.CategoryId && c.CompanyId == companyId && c.IsActive &&
            (c.InventoryId == article.InventoryId ||
             _db.Inventories.Any(i => i.Id == c.InventoryId && i.CompanyId == companyId && i.IsMaster))))
            return "La categoría no pertenece al catálogo de esta empresa o está inactiva.";
        if (article.BrandId.HasValue && !await _db.Brands.AnyAsync(b => b.Id == article.BrandId && b.CompanyId == companyId && b.CategoryId == article.CategoryId && b.IsActive))
            return "La marca no pertenece a esta categoría.";
        if (string.IsNullOrWhiteSpace(article.Code) || string.IsNullOrWhiteSpace(article.Name))
            return "Código y nombre del artículo son obligatorios.";
        if (article.Stock < 0) return "El stock no puede ser negativo.";
        return null;
    }

    [HttpPost]
    public async Task<IActionResult> PostArticle([FromBody] Article article)
    {
        if (!TryCompany(out var companyId)) return Unauthorized();
        if (!ModelState.IsValid) return BadRequest(ModelState);
        article.CompanyId = companyId;
        article.Id = 0;
        article.RegistrationDate = DateTime.UtcNow;
        var error = await ValidateMasterData(article, companyId);
        if (error != null) return BadRequest(new { error });
        if (await _db.Articles.AnyAsync(a => a.CompanyId == companyId && a.InventoryId == article.InventoryId && a.Code == article.Code && a.IsActive))
            return Conflict(new { error = "El código ya existe en este inventario." });
        if (article.Tracking.ToString() == "Serialized")
        {
            var units = article.Details.ToList();
            if (units.Any(d => string.IsNullOrWhiteSpace(d.SerialNumber)) ||
                units.Select(d => d.SerialNumber.Trim().ToUpperInvariant()).Distinct().Count() != units.Count ||
                article.Stock != units.Count)
                return BadRequest(new { error = "Un artículo serializado necesita una serie distinta por cada unidad de stock inicial." });
        }
        // EF administra la FK ArticleId de las series incluidas al crear.
        foreach (var d in article.Details) { d.Id = 0; d.IsActive = true; d.RegistrationDate = DateTime.UtcNow; }
        try
        {
            if (!await _service.CreateAsync(article)) return BadRequest(new { error = "No se pudo crear." });
            return CreatedAtAction(nameof(GetArticle), new { id = article.Id }, article);
        }
        catch (DbUpdateException)
        {
            return Conflict(new { error = "No se pudo guardar: verifica referencias y códigos duplicados." });
        }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> PutArticle(int id, [FromBody] Article update)
    {
        if (!TryCompany(out var companyId)) return Unauthorized();
        if (id != update.Id || !ModelState.IsValid) return BadRequest(ModelState);
        update.CompanyId = companyId;
        var error = await ValidateMasterData(update, companyId);
        if (error != null) return BadRequest(new { error });

        await using var tx = await _db.Database.BeginTransactionAsync();
        var current = await _db.Articles.FromSqlInterpolated(
            $"SELECT * FROM \"Articles\" WHERE \"Id\" = {id} AND \"CompanyId\" = {companyId} FOR UPDATE")
            .SingleOrDefaultAsync();
        if (current is null) return NotFound();
        await _db.Entry(current).Collection(a => a.Details).LoadAsync();
        if (!await AccessibleInventories(companyId, ObtenerUsuarioSeguro(), true).ContainsAsync(current.InventoryId)) return Forbid();
        if (!current.IsActive) return Conflict(new { error = "No se edita un artículo inactivo." });
        if (current.InventoryId != update.InventoryId || current.Tracking != update.Tracking)
            return Conflict(new { error = "No se puede cambiar el almacén ni el tipo de tracking de un artículo existente." });
        if (await _db.Articles.AnyAsync(a => a.Id != id && a.CompanyId == companyId && a.InventoryId == current.InventoryId && a.Code == update.Code && a.IsActive))
            return Conflict(new { error = "El código ya está utilizado por otro artículo." });
        if (await _db.ArticleAssignments.AnyAsync(a => a.ArticleId == id && a.CompanyId == companyId && a.Quantity > a.ReturnedQuantity) &&
            update.Stock != current.Stock)
            return Conflict(new { error = "Hay custodias vigentes: el stock se modifica con operaciones, no editando el artículo." });

        if (current.Tracking.ToString() == "Serialized" && update.Stock != current.Stock)
            return Conflict(new { error = "Para serializados, registra unidades en Gestión de series; no edites stock por cantidad." });
        var stockDelta = update.Stock - current.Stock;
        current.Name = update.Name.Trim(); current.Code = update.Code.Trim();
        current.Barcode = update.Barcode; current.Model = update.Model;
        current.Presentation = update.Presentation;
        current.SerialNumber = update.SerialNumber;
        current.CategoryId = update.CategoryId; current.BrandId = update.BrandId;
        current.MeasurementUnit = update.MeasurementUnit;
        current.AcquisitionUnit = update.AcquisitionUnit;
        current.SaleUnit = update.SaleUnit; current.ConversionFactor = update.ConversionFactor;
        current.AcquisitionPrice = update.AcquisitionPrice;
        current.AcquisitionCurrency = update.AcquisitionCurrency;
        current.SalePrice = update.SalePrice; current.SaleCurrency = update.SaleCurrency;
        current.Characteristics = update.Characteristics;
        current.Stock = update.Stock;
        current.ModificationDate = DateTime.UtcNow;

        if (current.Tracking.ToString() == "Serialized" && update.Details.Count > 0 && current.Details.Count == 1)
        {
            var from = update.Details.First(); var to = current.Details.First();
            to.SerialNumber = from.SerialNumber;
            to.Attr1 = from.Attr1; to.Attr2 = from.Attr2; to.Attr3 = from.Attr3;
            to.Attr4 = from.Attr4; to.Attr5 = from.Attr5; to.Attr6 = from.Attr6;
        }
        if (stockDelta != 0)
        {
            _db.Movements.Add(new Movement
            {
                ArticleId = id, CompanyId = companyId, MovementDate = DateTime.UtcNow,
                ActionId = stockDelta > 0 ? 1 : 2, Amount = Math.Abs(stockDelta),
                Observation = "Ajuste de stock desde edición de artículo"
            });
        }
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteArticle(int id)
    {
        if (!TryCompany(out var companyId)) return Unauthorized();
        var article = await _db.Articles.FirstOrDefaultAsync(a => a.Id == id && a.CompanyId == companyId);
        if (article is null) return NotFound();
        if (!await AccessibleInventories(companyId, ObtenerUsuarioSeguro(), true).ContainsAsync(article.InventoryId)) return Forbid();
        if (!article.IsActive) return NoContent();
        if (article.Stock != 0 || await _db.Movements.AnyAsync(m => m.ArticleId == id) ||
            await _db.SaleDetails.AnyAsync(d => d.ArticleId == id) ||
            await _db.ArticleAssignments.AnyAsync(a => a.ArticleId == id && a.CompanyId == companyId && a.Quantity > a.ReturnedQuantity))
            return Conflict(new { error = "Primero agota el stock y cierra las asignaciones pendientes." });
        article.IsActive = false;
        article.ModificationDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("count/inventory/{inventoryId:int}")]
    public async Task<IActionResult> GetArticleCount(int inventoryId)
    {
        if (!TryCompany(out var companyId)) return Unauthorized();
        if (!await AccessibleInventories(companyId, ObtenerUsuarioSeguro()).ContainsAsync(inventoryId)) return Forbid();
        return Ok(await _db.Articles.Where(a => a.InventoryId == inventoryId && a.CompanyId == companyId && a.IsActive)
            .SumAsync(a => a.Stock));
    }

    [HttpGet("barcode/{barcode}")]
    public async Task<IActionResult> GetArticleByBarcode(string barcode)
    {
        if (!TryCompany(out var companyId)) return Unauthorized();
        var permitted = AccessibleInventories(companyId, ObtenerUsuarioSeguro());
        var article = await _db.Articles.AsNoTracking().FirstOrDefaultAsync(a => a.CompanyId == companyId && permitted.Contains(a.InventoryId) && a.Barcode == barcode && a.IsActive);
        return article == null ? NotFound() : Ok(article);
    }

    [HttpPost("AddDetail")]
    public async Task<IActionResult> PostArticleDetail([FromBody] ArticleDetails detail)
    {
        if (!TryCompany(out var companyId)) return Unauthorized();
        var article = await _db.Articles.AsNoTracking().FirstOrDefaultAsync(a => a.Id == detail.ArticleId && a.CompanyId == companyId);
        if (article is null) return NotFound();
        if (!await AccessibleInventories(companyId, ObtenerUsuarioSeguro(), true).ContainsAsync(article.InventoryId)) return Forbid();
        if (article.Tracking.ToString() == "Serialized")
            return Conflict(new { error = "Registra unidades con POST /api/serial-units/{articleId}, que también crea el movimiento." });
        var result = await _service.AddDetailAsync(detail);
        return result.Success ? Ok(new { newId = detail.Id }) : BadRequest(new { error = result.ErrorMessage });
    }

    [HttpPut("UpdateDetail/{id:int}")]
    public async Task<IActionResult> UpdateDetail(int id, [FromBody] ArticleDetails detail)
    {
        if (!TryCompany(out var companyId)) return Unauthorized();
        if (id != detail.Id || !await _db.Articles.AnyAsync(a => a.Id == detail.ArticleId && a.CompanyId == companyId && AccessibleInventories(companyId, ObtenerUsuarioSeguro(), true).Contains(a.InventoryId))) return BadRequest();
        return await _service.UpdateDetailAsync(detail) ? NoContent() : BadRequest();
    }

    [HttpDelete("DeleteDetail/{id:int}")]
    public async Task<IActionResult> DeleteDetail(int id)
    {
        if (!TryCompany(out var companyId)) return Unauthorized();
        var detail = await _db.Set<ArticleDetails>().FirstOrDefaultAsync(x => x.Id == id);
        if (detail == null || !await _db.Articles.AnyAsync(a => a.Id == detail.ArticleId && a.CompanyId == companyId && AccessibleInventories(companyId, ObtenerUsuarioSeguro(), true).Contains(a.InventoryId))) return NotFound();
        if (await _db.ArticleUnitOperations.AnyAsync(o => o.ArticleDetailId == id))
            return Conflict(new { error = "La serie tiene historial de venta o asignación; no puede eliminarse." });
        return await _service.DeleteDetailAsync(id) ? NoContent() : BadRequest();
    }
}
