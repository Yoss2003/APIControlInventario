using InventoryAPI.Models;
using InventoryAPI.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace InventoryAPI.Controllers;

[Authorize]
[ApiController]
[Route("api/serial-units")]
public sealed class SerialUnitsController(AppDbContext db) : ControllerBase
{
    private bool Scope(out int companyId, out int userId)
    {
        userId = int.TryParse(User.FindFirst("UserId")?.Value, out var u) ? u : 0;
        companyId = int.TryParse(User.FindFirst("CompanyId")?.Value, out var c) ? c : 0;
        return userId > 0 && companyId > 0;
    }
    private IQueryable<int> Accessible(int cid, int uid, bool write)
    {
        var developer = User.FindFirst("RoleId")?.Value == "1";
        return db.Inventories.Where(i => i.CompanyId == cid &&
            (developer || i.UserId == uid || db.SharedInventories.Any(s => s.CompanyId == cid &&
                s.InventoryId == i.Id && s.UserId == uid &&
                (!write || s.AccessLevel == SharedInventory.AccessMode.Editor)))).Select(i => i.Id);
    }
    [HttpGet("{articleId:int}/available")]
    public async Task<IActionResult> AvailableUnits(int articleId)
    {
        if (!Scope(out var cid, out var uid)) return Unauthorized();
        var article = await db.Articles.AsNoTracking().FirstOrDefaultAsync(a => a.Id == articleId && a.CompanyId == cid);
        if (article is null) return NotFound();
        if (!await Accessible(cid, uid, false).ContainsAsync(article.InventoryId)) return Forbid();
        return Ok(await db.Set<ArticleDetails>().AsNoTracking()
            .Where(d => d.ArticleId == articleId && d.IsActive &&
                !db.ArticleUnitOperations.Any(op => op.ArticleDetailId == d.Id && !op.IsReversed))
            .OrderBy(d => d.SerialNumber)
            .Select(d => new { d.Id, d.SerialNumber, d.Attr1, d.Attr2, d.Attr3, d.Attr4, d.Attr5, d.Attr6 })
            .ToListAsync());
    }

    [HttpGet("assignment/{assignmentId:int}/pending")]
    public async Task<IActionResult> PendingAssignmentUnits(int assignmentId)
    {
        if (!Scope(out var cid, out var uid)) return Unauthorized();
        var assignment = await db.ArticleAssignments.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == assignmentId && a.CompanyId == cid);
        if (assignment is null) return NotFound();
        var developer = User.FindFirst("RoleId")?.Value == "1";
        var visible = developer || assignment.AssignedByUserId == uid || assignment.RecipientUserId == uid ||
            await db.Inventories.AnyAsync(i => i.Id == assignment.InventoryId && i.CompanyId == cid && i.UserId == uid);
        if (!visible) return Forbid();
        return Ok(await db.ArticleUnitOperations.AsNoTracking()
            .Where(op => op.AssignmentId == assignmentId && op.CompanyId == cid && !op.IsReversed)
            .Join(db.Set<ArticleDetails>(), op => op.ArticleDetailId, d => d.Id,
                (op, d) => new { id = d.Id, serialNumber = d.SerialNumber })
            .OrderBy(x => x.serialNumber).ToListAsync());
    }

    public sealed record RegisterUnitRequest([Required] string SerialNumber,
        string? Attr1, string? Attr2, string? Attr3, string? Attr4, string? Attr5, string? Attr6);
    [HttpPost("{articleId:int}")]
    public async Task<IActionResult> AddUnit(int articleId, [FromBody] RegisterUnitRequest input)
    {
        if (!Scope(out var cid, out var uid)) return Unauthorized();
        if (string.IsNullOrWhiteSpace(input.SerialNumber) || input.SerialNumber.Length > 150)
            return BadRequest(new { error = "Serie inválida." });
        await using var tx = await db.Database.BeginTransactionAsync();
        var article = await db.Articles.FromSqlInterpolated(
            $"SELECT * FROM \"Articles\" WHERE \"Id\" = {articleId} AND \"CompanyId\" = {cid} FOR UPDATE")
            .SingleOrDefaultAsync();
        if (article is null || !article.IsActive) return NotFound();
        if (!await Accessible(cid, uid, true).ContainsAsync(article.InventoryId)) return Forbid();
        if (article.Tracking.ToString() != "Serialized") return BadRequest(new { error = "Solo artículos serializados." });
        var serial = input.SerialNumber.Trim();
        if (await db.Set<ArticleDetails>().AnyAsync(d => d.ArticleId == articleId && d.SerialNumber == serial))
            return Conflict(new { error = "Serie repetida en este artículo." });
        var detail = new ArticleDetails { ArticleId = articleId, SerialNumber = serial,
            Attr1 = input.Attr1, Attr2 = input.Attr2, Attr3 = input.Attr3,
            Attr4 = input.Attr4, Attr5 = input.Attr5, Attr6 = input.Attr6,
            RegistrationDate = DateTime.UtcNow, IsActive = true };
        db.Set<ArticleDetails>().Add(detail);
        article.Stock += 1; article.ModificationDate = DateTime.UtcNow;
        db.Movements.Add(new Movement { ArticleId = articleId, CompanyId = cid, ActionId = 1,
            Amount = 1, MovementDate = DateTime.UtcNow, Observation = "Ingreso de unidad serializada: " + serial });
        await db.SaveChangesAsync(); await tx.CommitAsync();
        return Ok(new { detail.Id, detail.SerialNumber });
    }
}
