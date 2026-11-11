using InventoryAPI.Models;
using InventoryAPI.Data;
using InventoryAPI.Services;
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
        if (!SalePaymentResolver.TryResolve(sale.PaymentGroup, sale.DigitalWallet, sale.PaymentType,
            out var selectedPayment, out var paymentError))
            return BadRequest(new { error = paymentError });
        sale.PaymentType = selectedPayment;
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
        string? DigitalWallet = null, string? InstallmentFrequency = null, int? InstallmentCount = null,
        decimal? DownPayment = null, DateOnly? FirstDueDate = null);
    public sealed record CancelSaleRequest(string Reason);
    public sealed record InstallmentSimulationRequest(decimal TotalAmount, SaleInstallmentPlanRequest InstallmentPlan);
    public sealed record InstallmentReceiptRequest(decimal Amount, string PaymentMethod,
        string? DigitalWallet, string? Notes, Guid OperationKey);

    [HttpGet("payment-methods")]
    public IActionResult PaymentMethods() => Ok(new object[]
    {
        new { label = "Efectivo", value = "Efectivo" },
        new { label = "Tarjeta", value = "Tarjeta" },
        new { label = "Transferencia", value = "Transferencia" },
        new { label = "Billetera digital", value = "BilleteraDigital", wallets = new[] { "Yape", "Plin", "Bim" } },
        new { label = "Venta a Cuotas", value = "Cuotas", frequencies = new[] { "Diario", "Semanal", "Mensual" } }
    });

    [HttpPost("installments/simulate")]
    public IActionResult SimulateInstallments([FromBody] InstallmentSimulationRequest request)
    {
        if (!SaleInstallmentCalculator.TryBuild(request.TotalAmount, request.InstallmentPlan,
            SaleInstallmentCalculator.BusinessToday(), out var items, out var error))
            return BadRequest(new { error });
        return Ok(new
        {
            total = request.TotalAmount,
            downPayment = request.InstallmentPlan.DownPayment,
            financedAmount = request.TotalAmount - request.InstallmentPlan.DownPayment,
            installments = items.Select(i => new { i.InstallmentNumber, i.InstallmentAmount, i.DueDate, i.Frequency })
        });
    }

    [HttpGet("{id:int}/installments")]
    public async Task<IActionResult> GetSaleInstallments(int id)
    {
        int companyId = ObtenerEmpresaSegura();
        int userId = ObtenerUsuarioSeguro();
        var sale = await _db.Sales.AsNoTracking().Include(x => x.SaleDetails)
            .FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId);
        if (sale == null) return NotFound();

        var readable = ReadableInventories(companyId, userId);
        var articleIds = sale.SaleDetails.Select(d => d.ArticleId).ToArray();
        if (!await _db.Articles.AnyAsync(a => a.CompanyId == companyId &&
            articleIds.Contains(a.Id) && readable.Contains(a.InventoryId)))
            return Forbid();

        var items = await _db.SaleInstallments.AsNoTracking().Include(x => x.Receipts)
            .Where(x => x.SaleId == id && x.CompanyId == companyId)
            .OrderBy(x => x.InstallmentNumber).ToListAsync();
        var cancelled = await _db.SaleCancellations.AnyAsync(x => x.CompanyId == companyId && x.SaleId == id);
        var today = SaleInstallmentCalculator.BusinessToday();
        return Ok(new
        {
            saleId = id,
            totalAmount = sale.TotalAmount,
            downPayment = items.Count == 0 ? (decimal?)null : sale.TotalAmount - items.Sum(x => x.InstallmentAmount),
            financedAmount = items.Sum(x => x.InstallmentAmount),
            balance = items.Sum(x => x.InstallmentAmount - x.PaidAmount),
            cancelled,
            installments = items.Select(x => new
            {
                x.Id,
                x.InstallmentNumber,
                x.InstallmentAmount,
                x.PaidAmount,
                balance = x.InstallmentAmount - x.PaidAmount,
                x.DueDate,
                x.Frequency,
                status = cancelled ? "Cancelled" :
                    x.PaidAmount >= x.InstallmentAmount ? "Paid" :
                    x.DueDate < today ? "Overdue" : x.PaidAmount > 0 ? "Partial" : "Pending",
                receipts = x.Receipts.OrderBy(p => p.PaidAt).Select(p => new
                {
                    p.Id, p.Amount, p.PaidAt, paymentMethod = p.PaymentType.ToString(),
                    p.ReceivedByUserId, p.Notes
                })
            })
        });
    }

    [HttpPost("{id:int}/installments/{installmentId:int}/payments")]
    public async Task<IActionResult> PaySaleInstallment(int id, int installmentId, [FromBody] InstallmentReceiptRequest request)
    {
        int companyId = ObtenerEmpresaSegura();
        int userId = ObtenerUsuarioSeguro();
        if (companyId <= 0 || userId <= 0) return Unauthorized();
        if (request.OperationKey == Guid.Empty || request.Amount <= 0 ||
            decimal.Round(request.Amount, 2) != request.Amount)
            return BadRequest(new { error = "El abono debe ser positivo, con máximo dos decimales y clave de operación." });
        if (string.IsNullOrWhiteSpace(request.PaymentMethod) ||
            !SalePaymentResolver.TryResolve(request.PaymentMethod, request.DigitalWallet, PaymentType.Efectivo,
                out var paymentType, out _))
            return BadRequest(new { error = "Selecciona un método de abono válido y su billetera cuando corresponda." });
        if (paymentType == PaymentType.Cuotas)
            return BadRequest(new { error = "Un abono debe utilizar un método de pago efectivo, tarjeta, transferencia o billetera." });

        var sale = await _db.Sales.Include(s => s.SaleDetails)
            .FirstOrDefaultAsync(s => s.Id == id && s.CompanyId == companyId);
        if (sale == null) return NotFound();
        var writable = WritableInventories(companyId, userId);
        foreach (var detail in sale.SaleDetails)
            if (!await _db.Articles.AnyAsync(a => a.CompanyId == companyId &&
                a.Id == detail.ArticleId && writable.Contains(a.InventoryId)))
                return Forbid();

        await using var tx = await _db.Database.BeginTransactionAsync();
        // Bloquear primero la venta: la anulación utiliza la misma fila de exclusión.
        await _db.Sales.FromSqlInterpolated(
            $"SELECT * FROM \"Sales\" WHERE \"Id\" = {id} AND \"CompanyId\" = {companyId} FOR UPDATE")
            .SingleAsync();
        var installment = await _db.SaleInstallments.FromSqlInterpolated(
            $"SELECT * FROM \"SaleInstallments\" WHERE \"Id\" = {installmentId} AND \"SaleId\" = {id} AND \"CompanyId\" = {companyId} FOR UPDATE")
            .SingleOrDefaultAsync();
        if (installment == null) return NotFound();
        var existing = await _db.SaleInstallmentReceipts.AsNoTracking()
            .FirstOrDefaultAsync(p => p.OperationKey == request.OperationKey);
        if (existing != null)
        {
            if (existing.CompanyId != companyId || existing.SaleInstallmentId != installmentId ||
                existing.Amount != request.Amount || existing.PaymentType != paymentType)
                return Conflict(new { error = "La clave de operación ya se utilizó con otro pago." });
            return Ok(new { receiptId = existing.Id, reused = true,
                balance = installment.InstallmentAmount - installment.PaidAmount });
        }
        if (await _db.SaleCancellations.AnyAsync(x => x.CompanyId == companyId && x.SaleId == id))
            return Conflict(new { error = "No se pueden abonar cuotas de una venta anulada." });
        if (request.Amount > installment.InstallmentAmount - installment.PaidAmount)
            return Conflict(new { error = "El abono supera el saldo pendiente de la cuota." });

        var receipt = new SaleInstallmentReceipt
        {
            SaleInstallmentId = installment.Id, CompanyId = companyId, Amount = request.Amount,
            PaidAt = DateTime.UtcNow, PaymentType = paymentType, ReceivedByUserId = userId,
            Notes = request.Notes?.Trim(), OperationKey = request.OperationKey
        };
        installment.PaidAmount += request.Amount;
        _db.SaleInstallmentReceipts.Add(receipt);
        try
        {
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch (DbUpdateException)
        {
            return Conflict(new { error = "No se pudo confirmar el abono. Consulta la operación antes de reintentar." });
        }
        return Ok(new { receiptId = receipt.Id, balance = installment.InstallmentAmount - installment.PaidAmount,
            paidAmount = installment.PaidAmount });
    }


    [HttpPost("quick")]
    public async Task<IActionResult> Quick([FromBody] QuickSaleRequest request)
    {
        var companyId = ObtenerEmpresaSegura();
        var userId = ObtenerUsuarioSeguro();
        if (companyId <= 0 || userId <= 0) return Unauthorized();
        if (request.OperationKey == Guid.Empty || request.ArticleId <= 0 || request.Quantity <= 0 || request.UnitPrice < 0)
            return BadRequest(new { error = "Artículo, cantidad y precio válidos son obligatorios." });
        if (string.IsNullOrWhiteSpace(request.PaymentMethod) ||
            !SalePaymentResolver.TryResolve(request.PaymentMethod, request.DigitalWallet, PaymentType.Efectivo,
                out var payment, out var paymentError))
            return BadRequest(new { error = "Método de pago inválido o billetera sin seleccionar." });
        List<SaleInstallment> schedule = [];
        var credit = payment == PaymentType.Cuotas;
        var downPayment = request.DownPayment ?? 0m;
        if (credit)
        {
            if (string.IsNullOrWhiteSpace(request.CustomerName) ||
                request.CustomerName.Trim().Equals("Público general", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { error = "Identifica al cliente para registrar una venta a cuotas." });
            var creditRequest = new SaleInstallmentPlanRequest
            {
                InstallmentCount = request.InstallmentCount ?? 0,
                DownPayment = downPayment,
                Frequency = request.InstallmentFrequency ?? string.Empty,
                FirstDueDate = request.FirstDueDate
            };
            if (!SaleInstallmentCalculator.TryBuild(request.Quantity * request.UnitPrice, creditRequest,
                SaleInstallmentCalculator.BusinessToday(), out schedule, out var creditError))
                return BadRequest(new { error = creditError });
        }
        else if (request.InstallmentCount != null || request.DownPayment != null ||
            request.FirstDueDate != null || !string.IsNullOrWhiteSpace(request.InstallmentFrequency))
            return BadRequest(new { error = "La configuración de cuotas solamente corresponde a ventas financiadas." });
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
        var modeId = credit
            ? await _db.SalesModes.Where(x => x.SalesModeName == schedule[0].Frequency)
                .Select(x => x.Id).FirstOrDefaultAsync()
            : await _db.SalesModes.Select(x => x.Id).OrderBy(x => x).FirstOrDefaultAsync();
        if (modeId == 0)
        {
            var mode = new SalesMode { SalesModeName = credit ? schedule[0].Frequency : "Venta mostrador" };
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
            AmountReceived = credit ? downPayment : total, ChangeGiven = 0,
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
        // El POS rápido también deja cuotas persistidas dentro de la transacción de stock.
        foreach (var installment in schedule)
        {
            installment.SaleId = sale.Id;
            installment.CompanyId = companyId;
        }
        _db.SaleInstallments.AddRange(schedule);
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return Ok(new
        {
            saleId = sale.Id, total, amountReceived = sale.AmountReceived,
            financedAmount = credit ? total - downPayment : 0m,
            installmentCount = schedule.Count
        });
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, [FromBody] CancelSaleRequest request)
    {
        var companyId = ObtenerEmpresaSegura();
        var userId = ObtenerUsuarioSeguro();
        if (companyId <= 0 || userId <= 0) return Unauthorized();
        if (string.IsNullOrWhiteSpace(request.Reason)) return BadRequest(new { error = "Escribe un motivo." });
        await using var tx = await _db.Database.BeginTransactionAsync();
        // Sincronizar anulación con los abonos para no cancelar ventas ya cobradas.
        var lockedSale = await _db.Sales.FromSqlInterpolated(
            $"SELECT * FROM \"Sales\" WHERE \"Id\" = {id} AND \"CompanyId\" = {companyId} FOR UPDATE")
            .SingleOrDefaultAsync();
        if (lockedSale == null) return NotFound();
        var sale = await _db.Sales.Include(x => x.SaleDetails)
            .FirstOrDefaultAsync(s => s.Id == id && s.CompanyId == companyId);
        if (sale == null) return NotFound();
        var allowed = WritableInventories(companyId, userId);
        foreach (var detail in sale.SaleDetails)
            if (!await _db.Articles.AnyAsync(a => a.Id == detail.ArticleId && allowed.Contains(a.InventoryId))) return Forbid();
        if (await _db.SaleCancellations.AnyAsync(c => c.SaleId == id))
            return Conflict(new { error = "Venta ya anulada." });
        if (await _db.SaleInstallments.AnyAsync(i => i.SaleId == id && i.CompanyId == companyId) &&
            (sale.AmountReceived > 0m || await _db.SaleInstallmentReceipts.AnyAsync(p =>
                p.CompanyId == companyId && _db.SaleInstallments.Any(i => i.Id == p.SaleInstallmentId && i.SaleId == id))))
            return Conflict(new { error = "La venta a cuotas tiene cobros registrados. Gestiona los reembolsos antes de anularla." });
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
