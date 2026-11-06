using InventoryAPI.Models;
using InventoryAPI.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryAPI.Controllers;

[Authorize]
[ApiController]
[Route("api/Assignments")]
public sealed class AssignmentsController(AppDbContext db, IWebHostEnvironment environment) : ControllerBase
{
    private bool IsDeveloper => User.FindFirst("RoleId")?.Value == "1";
    private readonly AppDbContext _db = db;
    private readonly IWebHostEnvironment _env = environment;

    public sealed record AssignRequest(
        int ArticleId, decimal Quantity, string RecipientName, int? RecipientUserId,
        string? Observation, Guid OperationKey, List<int>? DetailIds);
    public sealed record ReturnRequest(decimal Quantity, string? Observation, Guid OperationKey, List<int>? DetailIds);

    private bool TryScope(out int companyId, out int userId)
    {
        companyId = 0; userId = 0;
        return int.TryParse(User.FindFirst("CompanyId")?.Value, out companyId) && companyId > 0 &&
            int.TryParse(User.FindFirst("UserId")?.Value, out userId) && userId > 0;
    }

    private IQueryable<ArticleAssignment> Visible(int companyId, int userId) =>
        _db.ArticleAssignments.AsNoTracking().Where(a => a.CompanyId == companyId &&
            (IsDeveloper || a.AssignedByUserId == userId || a.RecipientUserId == userId ||
             _db.Inventories.Any(i => i.Id == a.InventoryId && i.UserId == userId)));

    public sealed record AssignmentSummary(
        int Id, int ArticleId, int InventoryId, string RecipientName, int? RecipientUserId,
        int AssignedByUserId, DateTime AssignedAt, decimal Quantity, decimal ReturnedQuantity,
        decimal RemainingQuantity, string Status, string? Observation, bool HasEvidence,
        string? ArticleName, string? ArticleCode, string? MeasurementUnit);

    private IQueryable<AssignmentSummary> Display(IQueryable<ArticleAssignment> query) =>
        query.Select(a => new AssignmentSummary(
            a.Id, a.ArticleId, a.InventoryId, a.RecipientName, a.RecipientUserId,
            a.AssignedByUserId, a.AssignedAt, a.Quantity, a.ReturnedQuantity,
            a.Quantity - a.ReturnedQuantity,
            a.Quantity <= a.ReturnedQuantity ? "Devuelto" : "Asignado",
            a.Observation, a.EvidencePath != null,
            _db.Articles.Where(t => t.Id == a.ArticleId).Select(t => t.Name).FirstOrDefault(),
            _db.Articles.Where(t => t.Id == a.ArticleId).Select(t => t.Code).FirstOrDefault(),
            _db.Articles.Where(t => t.Id == a.ArticleId).Select(t => t.MeasurementUnit).FirstOrDefault()
        ));

    [HttpGet("outgoing")]
    public async Task<IActionResult> Outgoing()
    {
        if (!TryScope(out var companyId, out var userId)) return Unauthorized();
        var query = Visible(companyId, userId).Where(a => IsDeveloper || a.AssignedByUserId == userId ||
            _db.Inventories.Any(i => i.Id == a.InventoryId && i.UserId == userId));
        return Ok(await Display(query.OrderByDescending(a => a.AssignedAt)).ToListAsync());
    }

    [HttpGet("received")]
    public async Task<IActionResult> Received()
    {
        if (!TryScope(out var companyId, out var userId)) return Unauthorized();
        return Ok(await Display(_db.ArticleAssignments.AsNoTracking()
            .Where(a => a.CompanyId == companyId && a.RecipientUserId == userId)
            .OrderByDescending(a => a.AssignedAt)).ToListAsync());
    }

    [HttpGet("{id:int}/events")]
    public async Task<IActionResult> Events(int id)
    {
        if (!TryScope(out var companyId, out var userId)) return Unauthorized();
        if (!await Visible(companyId, userId).AnyAsync(a => a.Id == id)) return NotFound();
        return Ok(await _db.ArticleAssignmentEvents.AsNoTracking()
            .Where(e => e.AssignmentId == id && e.CompanyId == companyId)
            .OrderBy(e => e.OccurredAt)
            .Select(e => new { e.Id, e.Kind, e.Quantity, e.OccurredAt, e.Observation, e.ActorUserId, e.MovementId })
            .ToListAsync());
    }

    [HttpGet("recipients")]
    public async Task<IActionResult> Recipients([FromQuery] string? query)
    {
        if (!TryScope(out var companyId, out var userId)) return Unauthorized();
        var users = _db.Users.AsNoTracking().Where(u => u.CompanyId == companyId && u.Id != userId);
        if (!string.IsNullOrWhiteSpace(query))
            users = users.Where(u => u.Username != null && EF.Functions.ILike(u.Username, "%" + query.Trim() + "%"));
        return Ok(await users.OrderBy(u => u.Username)
            .Take(30).Select(u => new { id = u.Id, name = u.Username }).ToListAsync());
    }

    [HttpPost]
    public async Task<IActionResult> Assign([FromBody] AssignRequest request)
    {
        if (!TryScope(out var companyId, out var userId)) return Unauthorized();
        if (request.OperationKey == Guid.Empty || request.Quantity <= 0 ||
            string.IsNullOrWhiteSpace(request.RecipientName))
            return BadRequest(new { error = "Artículo, cantidad, destinatario y operación son obligatorios." });

        var prior = await _db.ArticleAssignments.AsNoTracking()
            .FirstOrDefaultAsync(a => a.OperationKey == request.OperationKey);
        if (prior != null)
            return prior.CompanyId == companyId && prior.AssignedByUserId == userId
                ? Ok(new { id = prior.Id, reused = true }) : Conflict(new { error = "Operación duplicada." });

        if (request.RecipientUserId is int recipientId &&
            !await _db.Users.AnyAsync(u => u.Id == recipientId && u.CompanyId == companyId && u.Id != userId))
            return BadRequest(new { error = "El usuario destinatario no pertenece a tu empresa." });

        await using var transaction = await _db.Database.BeginTransactionAsync();
        var article = await _db.Articles.FromSqlInterpolated(
            $"SELECT * FROM \"Articles\" WHERE \"Id\" = {request.ArticleId} AND \"CompanyId\" = {companyId} FOR UPDATE")
            .SingleOrDefaultAsync();
        if (article == null || !article.IsActive) return NotFound(new { error = "Artículo no encontrado o inactivo." });
        if (!await _db.Inventories.AnyAsync(i => i.Id == article.InventoryId && i.CompanyId == companyId && (i.UserId == userId || IsDeveloper)))
            return Forbid();
        if (article.Stock < request.Quantity) return Conflict(new { error = "Stock insuficiente." });
        if (article.Tracking.ToString() == "Serialized" && request.Quantity != decimal.Truncate(request.Quantity))
            return BadRequest(new { error = "La cantidad serializada debe ser entera." });

        var detailIds = request.DetailIds ?? new List<int>();
        if (article.Tracking.ToString() == "Serialized")
        {
            if (detailIds.Count != (int)request.Quantity || detailIds.Distinct().Count() != detailIds.Count)
                return BadRequest(new { error = "Selecciona exactamente una serie distinta por unidad asignada." });
            var available = await _db.Set<ArticleDetails>().CountAsync(d => d.ArticleId == article.Id && d.IsActive &&
                detailIds.Contains(d.Id) && !_db.ArticleUnitOperations.Any(op => op.ArticleDetailId == d.Id && !op.IsReversed));
            if (available != detailIds.Count) return Conflict(new { error = "Una de las series ya está vendida o asignada." });
        }
        else if (detailIds.Count > 0) return BadRequest(new { error = "Este artículo no usa series." });
        article.Stock -= request.Quantity;
        article.ModificationDate = DateTime.UtcNow;
        var assignment = new ArticleAssignment
        {
            OperationKey = request.OperationKey,
            CompanyId = companyId,
            InventoryId = article.InventoryId,
            ArticleId = article.Id,
            RecipientUserId = request.RecipientUserId,
            RecipientName = request.RecipientName.Trim(),
            AssignedByUserId = userId,
            Quantity = request.Quantity,
            Observation = request.Observation?.Trim(),
            AssignedAt = DateTime.UtcNow
        };
        var employeeId = await _db.Employees.Where(e => e.CompanyId == companyId && e.UserId == userId)
            .Select(e => (int?)e.Id).FirstOrDefaultAsync();
        var movement = new Movement
        {
            ArticleId = article.Id, CompanyId = companyId, EmployeeId = employeeId, ActionId = 3,
            MovementDate = DateTime.UtcNow, Amount = request.Quantity,
            Recipient = assignment.RecipientName,
            Observation = $"Entrega de custodia. {request.Observation}".Trim(),
            SalePrice = 0m
        };
        _db.ArticleAssignments.Add(assignment);
        _db.Movements.Add(movement);
        await _db.SaveChangesAsync();
        foreach (var detailId in detailIds)
            _db.ArticleUnitOperations.Add(new ArticleUnitOperation {
                CompanyId = companyId, ArticleId = article.Id, ArticleDetailId = detailId,
                AssignmentId = assignment.Id, CreatedAt = DateTime.UtcNow
            });
        _db.ArticleAssignmentEvents.Add(new ArticleAssignmentEvent
        {
            AssignmentId = assignment.Id, CompanyId = companyId, ActorUserId = userId,
            Kind = "Entrega", Quantity = request.Quantity, OccurredAt = DateTime.UtcNow,
            MovementId = movement.Id, Observation = request.Observation, OperationKey = request.OperationKey
        });
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();
        return CreatedAtAction(nameof(Events), new { id = assignment.Id }, new { id = assignment.Id });
    }

    [HttpPost("{id:int}/evidence")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> UploadEvidence(int id, IFormFile file)
    {
        if (!TryScope(out var companyId, out var userId)) return Unauthorized();
        var assignment = await _db.ArticleAssignments.FirstOrDefaultAsync(a => a.Id == id && a.CompanyId == companyId);
        if (assignment == null) return NotFound();
        if (assignment.AssignedByUserId != userId) return Forbid();
        if (file.Length is <= 0 or > 8_000_000) return BadRequest(new { error = "Imagen inválida (máx 8 MB)." });
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not (".jpg" or ".jpeg" or ".png" or ".webp"))
            return BadRequest(new { error = "Solo se aceptan imágenes." });
        var folder = Path.Combine(_env.ContentRootPath, "PrivateFiles", "assignments", companyId.ToString(), id.ToString());
        Directory.CreateDirectory(folder);
        var newName = Guid.NewGuid().ToString("N") + ext;
        var destination = Path.Combine(folder, newName);
        try
        {
            await using (var output = new FileStream(destination, FileMode.CreateNew))
                await file.CopyToAsync(output);
            assignment.EvidencePath = Path.Combine(companyId.ToString(), id.ToString(), newName).Replace('\\', '/');
            await _db.SaveChangesAsync();
            return Ok(new { evidence = true });
        }
        catch
        {
            if (System.IO.File.Exists(destination)) System.IO.File.Delete(destination);
            return StatusCode(500, new { error = "No se pudo almacenar la evidencia." });
        }
    }

    [HttpGet("{id:int}/evidence")]
    public async Task<IActionResult> GetEvidence(int id)
    {
        if (!TryScope(out var companyId, out var userId)) return Unauthorized();
        var assignment = await Visible(companyId, userId).FirstOrDefaultAsync(a => a.Id == id);
        if (assignment == null || string.IsNullOrWhiteSpace(assignment.EvidencePath)) return NotFound();
        var root = Path.GetFullPath(Path.Combine(_env.ContentRootPath, "PrivateFiles", "assignments"));
        var full = Path.GetFullPath(Path.Combine(root, assignment.EvidencePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!full.StartsWith(root + Path.DirectorySeparatorChar) || !System.IO.File.Exists(full)) return NotFound();
        var ext = Path.GetExtension(full).ToLowerInvariant();
        var type = ext switch { ".png" => "image/png", ".webp" => "image/webp", _ => "image/jpeg" };
        Response.Headers["Cache-Control"] = "private, no-store";
        return PhysicalFile(full, type);
    }

    [HttpPost("{id:int}/return")]
    public async Task<IActionResult> Return(int id, [FromBody] ReturnRequest request)
    {
        if (!TryScope(out var companyId, out var userId)) return Unauthorized();
        if (request.OperationKey == Guid.Empty || request.Quantity <= 0)
            return BadRequest(new { error = "Cantidad y clave de operación son obligatorias." });
        var old = await _db.ArticleAssignmentEvents.AsNoTracking()
            .FirstOrDefaultAsync(e => e.OperationKey == request.OperationKey);
        if (old != null)
            return old.CompanyId == companyId && old.AssignmentId == id
                ? Ok(new { reused = true }) : Conflict(new { error = "Operación duplicada." });

        await using var transaction = await _db.Database.BeginTransactionAsync();
        var assignment = await _db.ArticleAssignments.FromSqlInterpolated(
            $"SELECT * FROM \"ArticleAssignments\" WHERE \"Id\" = {id} AND \"CompanyId\" = {companyId} FOR UPDATE")
            .SingleOrDefaultAsync();
        if (assignment == null) return NotFound();
        var owns = IsDeveloper || assignment.AssignedByUserId == userId ||
            await _db.Inventories.AnyAsync(i => i.Id == assignment.InventoryId && i.UserId == userId && i.CompanyId == companyId);
        if (!owns && assignment.RecipientUserId != userId) return Forbid();
        if (assignment.Quantity - assignment.ReturnedQuantity < request.Quantity)
            return Conflict(new { error = "La devolución supera el saldo pendiente." });

        var article = await _db.Articles.FromSqlInterpolated(
            $"SELECT * FROM \"Articles\" WHERE \"Id\" = {assignment.ArticleId} AND \"CompanyId\" = {companyId} FOR UPDATE")
            .SingleOrDefaultAsync();
        if (article == null) return NotFound(new { error = "Artículo original no encontrado." });
        var returningIds = request.DetailIds ?? new List<int>();
        if (article.Tracking.ToString() == "Serialized")
        {
            if (returningIds.Count != (int)request.Quantity || returningIds.Distinct().Count() != returningIds.Count)
                return BadRequest(new { error = "Selecciona una serie por cada unidad devuelta." });
            var units = await _db.ArticleUnitOperations.Where(u => u.AssignmentId == id && !u.IsReversed &&
                returningIds.Contains(u.ArticleDetailId)).ToListAsync();
            if (units.Count != returningIds.Count) return Conflict(new { error = "Una serie no pertenece a esta custodia o ya fue devuelta." });
            foreach (var unit in units) { unit.IsReversed = true; unit.ReversedAt = DateTime.UtcNow; }
        }
        else if (returningIds.Count > 0) return BadRequest(new { error = "Artículo sin series." });
        article.Stock += request.Quantity;
        article.ModificationDate = DateTime.UtcNow;
        assignment.ReturnedQuantity += request.Quantity;
        var employeeId = await _db.Employees.Where(e => e.CompanyId == companyId && e.UserId == userId)
            .Select(e => (int?)e.Id).FirstOrDefaultAsync();
        var movement = new Movement
        {
            ArticleId = article.Id, CompanyId = companyId, EmployeeId = employeeId, ActionId = 1,
            MovementDate = DateTime.UtcNow, Amount = request.Quantity,
            Recipient = assignment.RecipientName, Observation = $"Devolución de custodia #{id}. {request.Observation}".Trim()
        };
        _db.Movements.Add(movement);
        await _db.SaveChangesAsync();
        _db.ArticleAssignmentEvents.Add(new ArticleAssignmentEvent
        {
            AssignmentId = assignment.Id, CompanyId = companyId, ActorUserId = userId,
            Kind = "Devolucion", Quantity = request.Quantity, OccurredAt = DateTime.UtcNow,
            MovementId = movement.Id, Observation = request.Observation, OperationKey = request.OperationKey
        });
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();
        return Ok(new { assignment.Id, assignment.ReturnedQuantity, remainingQuantity = assignment.Quantity - assignment.ReturnedQuantity });
    }
}
