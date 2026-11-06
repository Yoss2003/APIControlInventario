using InventoryAPI.Models;
using InventoryAPI.Data;
using InventoryAPI.Services.IServices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryAPI.Controllers;

public class SalesController(ISaleService saleService, AppDbContext db) : BaseApiController
{
    private bool IsDeveloper => User.FindFirst("RoleId")?.Value == "1";
    private readonly ISaleService _saleService = saleService;
    private readonly AppDbContext _db = db;
    private IQueryable<int> WritableInventories(int company, int userId) =>
        _db.Inventories.Where(i => i.CompanyId == company &&
            (IsDeveloper || i.UserId == userId || _db.SharedInventories.Any(s =>
                s.CompanyId == company && s.InventoryId == i.Id && s.UserId == userId &&
                s.AccessLevel == SharedInventory.AccessMode.Editor))).Select(i => i.Id);

    private IQueryable<int> ReadableInventories(int company, int userId) =>
        _db.Inventories.Where(i => i.CompanyId == company &&
            (IsDeveloper || i.UserId == userId || _db.SharedInventories.Any(s =>
                s.CompanyId == company && s.InventoryId == i.Id && s.UserId == userId))).Select(i => i.Id);


    [HttpGet]
    public async Task<IActionResult> GetSales()
    {
        var company = ObtenerEmpresaSegura(); var accessible = ReadableInventories(company, ObtenerUsuarioSeguro());
        return Ok(await _db.Sales.AsNoTracking().Include(s => s.SaleDetails)
            .Where(s => s.CompanyId == company && s.SaleDetails.Any(d =>
                _db.Articles.Any(a => a.Id == d.ArticleId && accessible.Contains(a.InventoryId))))
            .OrderByDescending(s => s.SaleDate).ToListAsync());
    }

    [HttpPost]
    public async Task<IActionResult> CreateSale([FromBody] Sale sale)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        sale.CompanyId = ObtenerEmpresaSegura();
        sale.UserId = ObtenerUsuarioSeguro();
        if (sale.SaleDetails == null || sale.SaleDetails.Count == 0) return BadRequest(new { error = "La venta no tiene artículos." });
        var permitted = WritableInventories(sale.CompanyId, sale.UserId);
        foreach (var d in sale.SaleDetails)
        {
            if (!await _db.Articles.AnyAsync(a => a.Id == d.ArticleId && a.CompanyId == sale.CompanyId && permitted.Contains(a.InventoryId)))
                return Forbid();
            d.CompanyId = sale.CompanyId;
        }
        var (ok, message) = await _saleService.ProcessSaleAsync(sale);
        return ok ? Ok(new { message, saleId = sale.Id }) : BadRequest(new { error = message });
    }

    [HttpGet("cancellations")]
    public async Task<IActionResult> Cancellations()
    {
        var companyId = ObtenerEmpresaSegura();
        return Ok(await _db.SaleCancellations.AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .Select(x => new { x.SaleId, x.CancelledAt, x.Reason })
            .ToListAsync());
    }

    public sealed record QuickSaleRequest(int ArticleId, decimal Quantity, decimal UnitPrice,
        string? CustomerName, string PaymentMethod, string? Notes, Guid OperationKey, List<int>? DetailIds);
    public sealed record CancelSaleRequest(string Reason);

    [HttpPost("quick")]
    public async Task<IActionResult> Quick([FromBody] QuickSaleRequest request)
    {
        var companyId = ObtenerEmpresaSegura();
        var userId = ObtenerUsuarioSeguro();
        if (companyId <= 0 || userId <= 0) return Unauthorized();
        if (request.OperationKey == Guid.Empty || request.ArticleId <= 0 || request.Quantity <= 0 || request.UnitPrice < 0)
            return BadRequest(new { error = "Artículo, cantidad y precio válidos son obligatorios." });
        if (!Enum.TryParse<PaymentType>(request.PaymentMethod, true, out var payment))
            return BadRequest(new { error = "Método de pago desconocido." });
        var prior = await _db.SaleOperations.FirstOrDefaultAsync(x => x.OperationKey == request.OperationKey);
        if (prior != null) return prior.CompanyId == companyId ? Ok(new { saleId = prior.SaleId, reused = true }) : Conflict();

        await using var tx = await _db.Database.BeginTransactionAsync();
        var article = await _db.Articles.FromSqlInterpolated(
            $"SELECT * FROM \"Articles\" WHERE \"Id\" = {request.ArticleId} AND \"CompanyId\" = {companyId} FOR UPDATE")
            .SingleOrDefaultAsync();
        if (article == null || !article.IsActive) return NotFound(new { error = "Artículo inactivo o no encontrado." });
        if (!await WritableInventories(companyId, userId).ContainsAsync(article.InventoryId)) return Forbid();
        if (article.Stock < request.Quantity) return Conflict(new { error = "Stock insuficiente." });
        if (article.Tracking.ToString() == "Serialized" && request.Quantity != decimal.Truncate(request.Quantity))
            return BadRequest(new { error = "Para artículos serializados se requieren unidades enteras." });
        var detailIds = request.DetailIds ?? new List<int>();
        if (article.Tracking.ToString() == "Serialized")
        {
            if (detailIds.Count != (int)request.Quantity || detailIds.Distinct().Count() != detailIds.Count)
                return BadRequest(new { error = "Selecciona una serie distinta por cada unidad vendida." });
            var usable = await _db.Set<ArticleDetails>().CountAsync(d => d.ArticleId == article.Id && d.IsActive &&
                detailIds.Contains(d.Id) && !_db.ArticleUnitOperations.Any(op => op.ArticleDetailId == d.Id && !op.IsReversed));
            if (usable != detailIds.Count) return Conflict(new { error = "Alguna de las series no está disponible." });
        }
        else if (detailIds.Count > 0) return BadRequest(new { error = "La selección de series solo aplica a artículos serializados." });
        var modeId = await _db.SalesModes.Select(x => x.Id).OrderBy(x => x).FirstOrDefaultAsync();
        if (modeId == 0)
        {
            var mode = new SalesMode { SalesModeName = "Venta mostrador" };
            _db.SalesModes.Add(mode);
            await _db.SaveChangesAsync();
            modeId = mode.Id;
        }
        var total = request.Quantity * request.UnitPrice;
        var sale = new Sale
        {
            UserId = userId, CompanyId = companyId, SalesModeId = modeId,
            SaleDate = DateTime.UtcNow, CustomerName = string.IsNullOrWhiteSpace(request.CustomerName) ? "Público general" : request.CustomerName.Trim(),
            PaymentType = payment, TotalAmount = total, Notes = request.Notes,
            AmountReceived = total, ChangeGiven = 0,
            SaleDetails = [new SaleDetail
            {
                ArticleId = article.Id, CompanyId = companyId, Quantity = request.Quantity,
                UnitPrice = request.UnitPrice, SubTotal = total
            }]
        };
        _db.Sales.Add(sale);
        article.Stock -= request.Quantity;
        article.ModificationDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        var employeeId = await _db.Employees.Where(e => e.CompanyId == companyId && e.UserId == userId)
            .Select(e => (int?)e.Id).FirstOrDefaultAsync();
        _db.Movements.Add(new Movement
        {
            ArticleId = article.Id, CompanyId = companyId, EmployeeId = employeeId,
            ActionId = 2, MovementDate = DateTime.UtcNow, Amount = request.Quantity,
            Recipient = sale.CustomerName, PaymentMethod = payment.ToString(), SalePrice = request.UnitPrice,
            Observation = request.Notes, DocumentNumber = $"VENTA-{sale.Id}"
        });
        foreach (var detailId in detailIds)
            _db.ArticleUnitOperations.Add(new ArticleUnitOperation {
                CompanyId = companyId, ArticleId = article.Id, ArticleDetailId = detailId,
                SaleId = sale.Id, CreatedAt = DateTime.UtcNow
            });
        _db.SaleOperations.Add(new SaleOperation { OperationKey = request.OperationKey, SaleId = sale.Id, CompanyId = companyId });
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return Ok(new { saleId = sale.Id, total });
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, [FromBody] CancelSaleRequest request)
    {
        var companyId = ObtenerEmpresaSegura();
        var userId = ObtenerUsuarioSeguro();
        if (companyId <= 0 || userId <= 0) return Unauthorized();
        if (string.IsNullOrWhiteSpace(request.Reason)) return BadRequest(new { error = "Escribe un motivo." });
        await using var tx = await _db.Database.BeginTransactionAsync();
        var sale = await _db.Sales.Include(x => x.SaleDetails)
            .FirstOrDefaultAsync(s => s.Id == id && s.CompanyId == companyId);
        if (sale == null) return NotFound();
        var allowed = WritableInventories(companyId, userId);
        foreach (var detail in sale.SaleDetails)
            if (!await _db.Articles.AnyAsync(a => a.Id == detail.ArticleId && allowed.Contains(a.InventoryId))) return Forbid();
        if (await _db.SaleCancellations.AnyAsync(c => c.SaleId == id))
            return Conflict(new { error = "Venta ya anulada." });
        // Bloqueo de todas las filas de artículos en orden para evitar pérdidas de actualización.
        var keys = sale.SaleDetails.Select(d => d.ArticleId).Distinct().OrderBy(x => x).ToArray();
        foreach (var articleId in keys)
        {
            var article = await _db.Articles.FromSqlInterpolated(
                $"SELECT * FROM \"Articles\" WHERE \"Id\" = {articleId} AND \"CompanyId\" = {companyId} FOR UPDATE")
                .SingleOrDefaultAsync();
            if (article == null) return NotFound(new { error = "Falta uno de los artículos de la venta." });
            var quantity = sale.SaleDetails.Where(d => d.ArticleId == articleId).Sum(d => d.Quantity);
            article.Stock += quantity;
            article.ModificationDate = DateTime.UtcNow;
            _db.Movements.Add(new Movement
            {
                ArticleId = article.Id, CompanyId = companyId, ActionId = 1,
                MovementDate = DateTime.UtcNow, Amount = quantity,
                Observation = $"Anulación de venta #{id}: {request.Reason.Trim()}"
            });
        }
        var soldUnits = await _db.ArticleUnitOperations.Where(u => u.SaleId == id && !u.IsReversed).ToListAsync();
        foreach (var soldUnit in soldUnits) { soldUnit.IsReversed = true; soldUnit.ReversedAt = DateTime.UtcNow; }
        _db.SaleCancellations.Add(new SaleCancellation
        {
            SaleId = id, CompanyId = companyId, CancelledByUserId = userId,
            CancelledAt = DateTime.UtcNow, Reason = request.Reason.Trim()
        });
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return Ok(new { message = "Venta anulada, stock devuelto e historial conservado." });
    }
}
