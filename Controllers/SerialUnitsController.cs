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
        if (await db.Set<ArticleDetails>().AnyAsync(d => d.ArticleId == articleId &&
            d.SerialNumber.ToUpper() == serial.ToUpper()))
            return Conflict(new { error = "Serie repetida en este artículo." });

        var category = await db.Categories.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == article.CategoryId && c.CompanyId == cid && c.IsActive);
        if (category is null) return Conflict(new { error = "La categoría del artículo ya no está disponible." });
        var uniqueRules = new (string? Label, bool IsUnique, string? Value)[]
        {
            (category.Label1, category.IsUnique1, input.Attr1),
            (category.Label2, category.IsUnique2, input.Attr2),
            (category.Label3, category.IsUnique3, input.Attr3),
            (category.Label4, category.IsUnique4, input.Attr4),
            (category.Label5, category.IsUnique5, input.Attr5),
            (category.Label6, category.IsUnique6, input.Attr6)
        };
        var existingAttributes = await db.Set<ArticleDetails>().AsNoTracking()
            .Where(d => d.ArticleId == articleId)
            .Select(d => new { d.Attr1, d.Attr2, d.Attr3, d.Attr4, d.Attr5, d.Attr6 })
            .ToListAsync();
        for (var index = 0; index < uniqueRules.Length; index++)
        {
            var rule = uniqueRules[index];
            if (!rule.IsUnique || string.IsNullOrWhiteSpace(rule.Label)) continue;
            if (string.IsNullOrWhiteSpace(rule.Value))
                return BadRequest(new { error = $"El atributo «{rule.Label}» es obligatorio para esta unidad." });
            var value = rule.Value.Trim();
            var alreadyUsed = existingAttributes.Any(row =>
            {
                var previous = index switch
                {
                    0 => row.Attr1, 1 => row.Attr2, 2 => row.Attr3,
                    3 => row.Attr4, 4 => row.Attr5, _ => row.Attr6
                };
                return string.Equals(previous?.Trim(), value, StringComparison.OrdinalIgnoreCase);
            });
            if (alreadyUsed)
                return Conflict(new { error = $"El atributo «{rule.Label}» ya está utilizado en otra unidad." });
        }

        var now = DateTime.UtcNow;
        var detail = new ArticleDetails { ArticleId = articleId, SerialNumber = serial,
            Attr1 = input.Attr1?.Trim(), Attr2 = input.Attr2?.Trim(), Attr3 = input.Attr3?.Trim(),
            Attr4 = input.Attr4?.Trim(), Attr5 = input.Attr5?.Trim(), Attr6 = input.Attr6?.Trim(),
            StatusId = article.StatusId, RegistrationDate = now, IsActive = true };
        db.Set<ArticleDetails>().Add(detail);
        article.Stock += 1; article.ModificationDate = now;
        var username = User.Identity?.Name ?? User.FindFirst("Username")?.Value ?? "Usuario";
        db.Movements.Add(new Movement { ArticleId = articleId, CompanyId = cid, ActionId = 1,
            Amount = 1, MovementDate = now, Observation = "Ingreso de unidad serializada: " + serial });
        db.HistoryLogs.Add(new HistoryLog
        {
            LogDate = now, Username = username, ModuleName = "Inventario",
            ActionName = "Ingreso de serie",
            Detail = $"Se registró la serie «{serial}» del artículo «{article.Name}».",
            CompanyId = cid
        });
        await db.SaveChangesAsync(); await tx.CommitAsync();
        return Ok(new { detail.Id, detail.SerialNumber });
    }

    public sealed record RegisterUnitBatchRequest(List<RegisterUnitRequest> Units);

    /// <summary>Registra varias unidades serializadas en una transacción y un movimiento de entrada.</summary>
    [HttpPost("{articleId:int}/batch")]
    public async Task<IActionResult> AddUnitsBatch(int articleId, [FromBody] RegisterUnitBatchRequest input)
    {
        if (!Scope(out var cid, out var uid)) return Unauthorized();
        var units = input?.Units;
        if (units is null || units.Count is < 1 or > 1000)
            return BadRequest(new { error = "El lote debe contener entre 1 y 1000 unidades." });

        var normalized = units.Select(x => new RegisterUnitRequest(
            (x.SerialNumber ?? string.Empty).Trim(),
            x.Attr1?.Trim(), x.Attr2?.Trim(), x.Attr3?.Trim(),
            x.Attr4?.Trim(), x.Attr5?.Trim(), x.Attr6?.Trim())).ToList();

        if (normalized.Any(x => string.IsNullOrWhiteSpace(x.SerialNumber) || x.SerialNumber.Length > 150))
            return BadRequest(new { error = "Todas las unidades necesitan una serie válida de hasta 150 caracteres." });

        var submitted = normalized.Select(x => x.SerialNumber).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (submitted.Count != normalized.Count)
            return BadRequest(new { error = "El lote contiene números de serie duplicados." });

        await using var tx = await db.Database.BeginTransactionAsync();
        var article = await db.Articles.FromSqlInterpolated(
            $"SELECT * FROM \"Articles\" WHERE \"Id\" = {articleId} AND \"CompanyId\" = {cid} FOR UPDATE")
            .SingleOrDefaultAsync();
        if (article is null || !article.IsActive) return NotFound();
        if (!await Accessible(cid, uid, true).ContainsAsync(article.InventoryId)) return Forbid();
        if (article.Tracking.ToString() != "Serialized")
            return BadRequest(new { error = "Solo se permiten lotes para artículos serializados." });

        var category = await db.Categories.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == article.CategoryId && c.CompanyId == cid && c.IsActive);
        if (category is null) return Conflict(new { error = "La categoría del artículo ya no está disponible." });

        var existingDetails = await db.Set<ArticleDetails>().AsNoTracking()
            .Where(d => d.ArticleId == articleId)
            .Select(d => new { d.SerialNumber, d.Attr1, d.Attr2, d.Attr3, d.Attr4, d.Attr5, d.Attr6 })
            .ToListAsync();
        var existing = existingDetails.Select(d => d.SerialNumber)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var duplicate = normalized.FirstOrDefault(x => existing.Contains(x.SerialNumber));
        if (duplicate is not null)
            return Conflict(new { error = $"La serie «{duplicate.SerialNumber}» ya está registrada en este artículo." });

        var uniqueRules = new (string? Label, bool IsUnique, Func<RegisterUnitRequest, string?> GetValue,
            Func<dynamic, string?> GetExisting)[]
        {
            (category.Label1, category.IsUnique1, x => x.Attr1, x => x.Attr1),
            (category.Label2, category.IsUnique2, x => x.Attr2, x => x.Attr2),
            (category.Label3, category.IsUnique3, x => x.Attr3, x => x.Attr3),
            (category.Label4, category.IsUnique4, x => x.Attr4, x => x.Attr4),
            (category.Label5, category.IsUnique5, x => x.Attr5, x => x.Attr5),
            (category.Label6, category.IsUnique6, x => x.Attr6, x => x.Attr6)
        };
        foreach (var rule in uniqueRules)
        {
            if (!rule.IsUnique || string.IsNullOrWhiteSpace(rule.Label)) continue;
            var values = normalized.Select(rule.GetValue)
                .Select(x => x?.Trim() ?? string.Empty).ToList();
            if (values.Any(string.IsNullOrWhiteSpace))
                return BadRequest(new { error = $"Todas las unidades necesitan completar el atributo «{rule.Label}»." });
            var normalizedValues = values.Select(x => x.ToUpperInvariant()).ToList();
            if (normalizedValues.Distinct(StringComparer.Ordinal).Count() != normalizedValues.Count)
                return BadRequest(new { error = $"El atributo «{rule.Label}» debe ser diferente para cada unidad del lote." });
            var previouslyUsed = existingDetails.Select(rule.GetExisting)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var conflict = values.FirstOrDefault(previouslyUsed.Contains);
            if (conflict is not null)
                return Conflict(new { error = $"El atributo «{rule.Label}» con valor «{conflict}» ya está utilizado en otra unidad." });
        }

        var now = DateTime.UtcNow;
        var details = normalized.Select(x => new ArticleDetails
        {
            ArticleId = articleId,
            SerialNumber = x.SerialNumber,
            Attr1 = x.Attr1, Attr2 = x.Attr2, Attr3 = x.Attr3,
            Attr4 = x.Attr4, Attr5 = x.Attr5, Attr6 = x.Attr6,
            StatusId = article.StatusId,
            RegistrationDate = now,
            IsActive = true
        }).ToList();

        db.Set<ArticleDetails>().AddRange(details);
        article.Stock += details.Count;
        article.ModificationDate = now;

        var employeeId = int.TryParse(User.FindFirst("EmployeeId")?.Value, out var employee) && employee > 0
            ? employee : (int?)null;
        var username = User.Identity?.Name ?? User.FindFirst("Username")?.Value ?? "Usuario";
        db.Movements.Add(new Movement
        {
            ArticleId = articleId, EmployeeId = employeeId, CompanyId = cid,
            ActionId = 1, Amount = details.Count, MovementDate = now,
            SalePrice = 0, PaymentMethod = "N/A", Recipient = "Almacén Local",
            Observation = $"Ingreso en lote de {details.Count} unidades serializadas por {username}."
        });
        db.HistoryLogs.Add(new HistoryLog
        {
            LogDate = now, Username = username, ModuleName = "Inventario",
            ActionName = "Ingreso de series",
            Detail = $"Se registraron {details.Count} unidades serializadas del artículo «{article.Name}».",
            CompanyId = cid
        });

        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Ok(new
        {
            added = details.Count,
            stock = article.Stock,
            units = details.Select(x => new { x.Id, x.SerialNumber })
        });
    }

    public sealed record DecommissionUnitRequest([Required] int DetailId, [Required] string Observation);

    /// <summary>
    /// Da de baja una unidad serializada conservando su detalle y su historial.
    /// Una unidad con venta o asignación vigente no puede retirarse.
    /// </summary>
    [HttpPost("{articleId:int}/decommission")]
    public async Task<IActionResult> DecommissionUnit(int articleId, [FromBody] DecommissionUnitRequest input)
    {
        if (!Scope(out var cid, out var uid)) return Unauthorized();
        var reason = input?.Observation?.Trim() ?? string.Empty;
        if (input is null || input.DetailId <= 0 || reason.Length is < 5 or > 500)
            return BadRequest(new { error = "Indica la unidad y un motivo de baja de entre 5 y 500 caracteres." });

        await using var tx = await db.Database.BeginTransactionAsync();
        var article = await db.Articles.FromSqlInterpolated(
            $"SELECT * FROM \"Articles\" WHERE \"Id\" = {articleId} AND \"CompanyId\" = {cid} FOR UPDATE")
            .SingleOrDefaultAsync();
        if (article is null) return NotFound();
        if (!await Accessible(cid, uid, true).ContainsAsync(article.InventoryId)) return Forbid();
        if (!article.IsActive || article.Tracking.ToString() != "Serialized")
            return Conflict(new { error = "El artículo debe estar activo y ser serializado." });

        var detail = await db.Set<ArticleDetails>().FirstOrDefaultAsync(d =>
            d.Id == input.DetailId && d.ArticleId == articleId);
        if (detail is null) return NotFound(new { error = "La unidad no pertenece a este artículo." });
        if (!detail.IsActive) return Conflict(new { error = "La unidad ya fue retirada." });
        if (await db.ArticleUnitOperations.AnyAsync(op =>
            op.ArticleDetailId == detail.Id && op.CompanyId == cid && !op.IsReversed))
            return Conflict(new { error = "La unidad tiene una venta o asignación vigente. Cierra o revierte esa operación antes de darla de baja." });
        if (article.Stock < 1)
            return Conflict(new { error = "El stock del artículo no permite retirar otra unidad." });

        var now = DateTime.UtcNow;
        var employeeId = int.TryParse(User.FindFirst("EmployeeId")?.Value, out var employee) && employee > 0
            ? employee : (int?)null;
        var username = User.Identity?.Name ?? User.FindFirst("Username")?.Value ?? "Usuario";

        // No se elimina ArticleDetails: la serie permanece consultable como retirada.
        detail.IsActive = false;
        article.Stock -= 1;
        article.ModificationDate = now;
        db.Movements.Add(new Movement
        {
            ArticleId = articleId, EmployeeId = employeeId, CompanyId = cid,
            ActionId = 2, Amount = 1, MovementDate = now,
            SalePrice = 0, PaymentMethod = "N/A", Recipient = "Baja definitiva",
            Observation = $"Baja definitiva de serie {detail.SerialNumber}. Motivo: {reason}"
        });
        db.HistoryLogs.Add(new HistoryLog
        {
            LogDate = now, Username = username, ModuleName = "Inventario",
            ActionName = "Baja de unidad serializada",
            Detail = $"Serie «{detail.SerialNumber}» del artículo «{article.Name}». Motivo: {reason}",
            CompanyId = cid
        });

        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Ok(new { detail.Id, detail.SerialNumber, detail.IsActive, stock = article.Stock });
    }
}
