using InventoryAPI.Models;
using InventoryAPI.Data;
using InventoryAPI.Services.IServices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

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
        // La venta a crédito requiere un cronograma persistido junto a la venta.
        if (sale.PaymentType == PaymentType.Cuotas)
            return BadRequest(new { error = "Para ventas a cuotas utiliza POST /api/Sales/quick con la configuración del crédito." });
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
        string? CustomerName, string PaymentMethod, string? Notes, Guid OperationKey, List<int>? DetailIds,
        string? InstallmentFrequency = null, int? InstallmentCount = null, decimal? DownPayment = null);
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

        var credit = payment == PaymentType.Cuotas;
        var quotedTotal = request.Quantity * request.UnitPrice;
        var initialPayment = request.DownPayment.GetValueOrDefault();
        var installmentsCount = request.InstallmentCount.GetValueOrDefault();
        var creditFrequency = request.InstallmentFrequency?.Trim().ToLowerInvariant() switch
        {
            "diario" => "Diario",
            "semanal" => "Semanal",
            "mensual" => "Mensual",
            _ => null
        };
        if (credit)
        {
            if (quotedTotal <= 0 || decimal.Round(quotedTotal, 2) != quotedTotal)
                return BadRequest(new { error = "La venta a cuotas requiere un total positivo expresado en céntimos." });
            if (string.IsNullOrWhiteSpace(request.CustomerName) ||
                request.CustomerName.Trim().Equals("Público general", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { error = "Identifica al comprador para registrar una venta a cuotas." });
            if (creditFrequency is null || installmentsCount is < 1 or > 60)
                return BadRequest(new { error = "Selecciona frecuencia diaria, semanal o mensual y entre 1 y 60 cuotas." });
            if (initialPayment < 0 || initialPayment >= quotedTotal || decimal.Round(initialPayment, 2) != initialPayment)
                return BadRequest(new { error = "El adelanto debe ser válido, menor que el total y tener hasta 2 decimales." });
            if (quotedTotal - initialPayment < installmentsCount * 0.01m)
                return BadRequest(new { error = "El saldo financiado debe cubrir al menos S/ 0.01 por cuota." });
        }
        else if (request.InstallmentCount is not null || request.DownPayment is not null ||
                 !string.IsNullOrWhiteSpace(request.InstallmentFrequency))
            return BadRequest(new { error = "La configuración de cuotas solo aplica a pagos tipo Cuotas." });

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
            AmountReceived = credit ? initialPayment : total, ChangeGiven = 0,
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
        var saleMovement = new Movement
        {
            ArticleId = article.Id, CompanyId = companyId, EmployeeId = employeeId,
            ActionId = 2, MovementDate = DateTime.UtcNow, Amount = request.Quantity,
            Recipient = sale.CustomerName, PaymentMethod = payment.ToString(), SalePrice = request.UnitPrice,
            Observation = request.Notes, DocumentNumber = $"VENTA-{sale.Id}"
        };
        _db.Movements.Add(saleMovement);
        foreach (var detailId in detailIds)
            _db.ArticleUnitOperations.Add(new ArticleUnitOperation {
                CompanyId = companyId, ArticleId = article.Id, ArticleDetailId = detailId,
                SaleId = sale.Id, CreatedAt = DateTime.UtcNow
            });
        _db.SaleOperations.Add(new SaleOperation { OperationKey = request.OperationKey, SaleId = sale.Id, CompanyId = companyId });
        // Primero se confirma el movimiento en el contexto para disponer de su ID.
        // Todo sigue en la misma transacción PostgreSQL: si falla el cronograma se revierte stock, venta y movimiento.
        await _db.SaveChangesAsync();
        if (credit)
        {
            var financed = total - initialPayment;
            var ordinary = decimal.Floor(financed * 100m / installmentsCount) / 100m;
            var saleDay = sale.SaleDate.Date;
            for (var installment = 1; installment <= installmentsCount; installment++)
            {
                var amount = installment == installmentsCount
                    ? financed - ordinary * (installmentsCount - 1) : ordinary;
                var dueDate = creditFrequency switch
                {
                    "Diario" => saleDay.AddDays(installment),
                    "Semanal" => saleDay.AddDays(installment * 7),
                    _ => saleDay.AddMonths(installment)
                };
                _db.AccountsReceivables.Add(new AccountReceivable
                {
                    CompanyId = companyId,
                    MovementId = saleMovement.Id,
                    InstallmentNumber = installment,
                    InstallmentAmount = amount,
                    PaidAmount = 0m,
                    LateFeeAmount = 0m,
                    DueDate = dueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    Status = "Pending",
                    Frequency = creditFrequency!
                });
            }
            await _db.SaveChangesAsync();
        }
        await tx.CommitAsync();
        return Ok(new { saleId = sale.Id, total, amountReceived = sale.AmountReceived,
            financedAmount = credit ? total - initialPayment : 0m, installmentCount = credit ? installmentsCount : 0 });
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, [FromBody] CancelSaleRequest request)
    {
        var companyId = ObtenerEmpresaSegura();
        var userId = ObtenerUsuarioSeguro();
        if (companyId <= 0 || userId <= 0) return Unauthorized();
        if (string.IsNullOrWhiteSpace(request.Reason)) return BadRequest(new { error = "Escribe un motivo." });
        await using var tx = await _db.Database.BeginTransactionAsync();
        // Bloquear la misma fila que el registro de abonos, evitando anular y cobrar simultáneamente.
        var sale = await _db.Sales.FromSqlInterpolated(
            $"SELECT * FROM \"Sales\" WHERE \"Id\" = {id} AND \"CompanyId\" = {companyId} FOR UPDATE")
            .SingleOrDefaultAsync();
        if (sale == null) return NotFound();
        await _db.Entry(sale).Collection(x => x.SaleDetails).LoadAsync();
        var allowed = WritableInventories(companyId, userId);
        foreach (var detail in sale.SaleDetails)
            if (!await _db.Articles.AnyAsync(a => a.Id == detail.ArticleId && allowed.Contains(a.InventoryId))) return Forbid();
        if (await _db.SaleCancellations.AnyAsync(c => c.SaleId == id))
            return Conflict(new { error = "Venta ya anulada." });
        if (sale.PaymentType == PaymentType.Cuotas)
        {
            var movementIds = await _db.Movements.Where(m => m.CompanyId == companyId &&
                m.DocumentNumber == $"VENTA-{id}").Select(m => m.Id).ToListAsync();
            var schedule = await _db.AccountsReceivables.Where(c => c.CompanyId == companyId &&
                movementIds.Contains(c.MovementId)).ToListAsync();
            if (schedule.Count == 0)
                return Conflict(new { error = "La venta a cuotas no tiene cronograma válido. Requiere revisión antes de anular." });
            var installmentIds = schedule.Select(c => c.Id).ToArray();
            if (sale.AmountReceived.GetValueOrDefault() > 0 || schedule.Any(c => c.PaidAmount > 0) ||
                await _db.InstallmentPayments.AnyAsync(p => p.CompanyId == companyId && installmentIds.Contains(p.InstallmentId)))
                return Conflict(new { error = "La venta ya tiene adelantos o abonos. Registra primero la devolución del dinero mediante un proceso financiero." });
            foreach (var installment in schedule) installment.Status = "Cancelled";
        }
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
