using InventoryAPI.Data;
using InventoryAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace InventoryAPI.Controllers;

// Los cobros siempre se aplican a una cuenta por cobrar real, en una transacción.
// No se admiten altas/ediciones de pagos directas con un saldo sin verificar.
public sealed class InstallmentPaymentsController(AppDbContext db) : BaseApiController
{
    private readonly AppDbContext _db = db;
    private bool IsDeveloper => User.FindFirst("RoleId")?.Value == "1";

    private IQueryable<int> ReadableInventories(int company, int userId) =>
        _db.Inventories.Where(i => i.CompanyId == company &&
            (IsDeveloper || i.UserId == userId || _db.SharedInventories.Any(s =>
                s.CompanyId == company && s.InventoryId == i.Id && s.UserId == userId)))
            .Select(i => i.Id);

    private IQueryable<int> WritableInventories(int company, int userId) =>
        _db.Inventories.Where(i => i.CompanyId == company &&
            (IsDeveloper || i.UserId == userId || _db.SharedInventories.Any(s =>
                s.CompanyId == company && s.InventoryId == i.Id && s.UserId == userId &&
                s.AccessLevel == SharedInventory.AccessMode.Editor)))
            .Select(i => i.Id);

    public sealed record RecordPaymentRequest(int InstallmentId, decimal PaymentAmount, string? Observation);

    [HttpGet]
    public async Task<IActionResult> GetInstallmentPayments()
    {
        var companyId = ObtenerEmpresaSegura();
        var userId = ObtenerUsuarioSeguro();
        var readable = ReadableInventories(companyId, userId);
        var allowedAccounts = _db.AccountsReceivables.Where(a => a.CompanyId == companyId &&
            _db.Movements.Any(m => m.Id == a.MovementId && m.CompanyId == companyId &&
                _db.Articles.Any(article => article.Id == m.ArticleId && readable.Contains(article.InventoryId))))
            .Select(a => a.Id);
        return Ok(await _db.InstallmentPayments.AsNoTracking()
            .Where(p => p.CompanyId == companyId && allowedAccounts.Contains(p.InstallmentId))
            .OrderByDescending(p => p.Id).ToListAsync());
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetInstallmentPayment(int id)
    {
        var companyId = ObtenerEmpresaSegura();
        var userId = ObtenerUsuarioSeguro();
        var payment = await _db.InstallmentPayments.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id && p.CompanyId == companyId);
        if (payment == null) return NotFound();
        var allowed = ReadableInventories(companyId, userId);
        var permitted = await _db.AccountsReceivables.AnyAsync(a => a.Id == payment.InstallmentId && a.CompanyId == companyId &&
            _db.Movements.Any(m => m.Id == a.MovementId && m.CompanyId == companyId &&
                _db.Articles.Any(article => article.Id == m.ArticleId && allowed.Contains(article.InventoryId))));
        return permitted ? Ok(payment) : Forbid();
    }

    // Permite consultar el cronograma de una venta y los abonos aplicados a sus cuotas.
    [HttpGet("sale/{saleId:int}")]
    public async Task<IActionResult> GetSaleInstallments(int saleId)
    {
        var companyId = ObtenerEmpresaSegura();
        var userId = ObtenerUsuarioSeguro();
        var sale = await _db.Sales.AsNoTracking().FirstOrDefaultAsync(s => s.Id == saleId && s.CompanyId == companyId);
        if (sale == null) return NotFound();
        var readable = ReadableInventories(companyId, userId);
        if (!await _db.SaleDetails.AnyAsync(d => d.SaleId == saleId && d.CompanyId == companyId &&
            _db.Articles.Any(a => a.Id == d.ArticleId && readable.Contains(a.InventoryId))))
            return Forbid();

        var movementIds = await _db.Movements.AsNoTracking()
            .Where(m => m.CompanyId == companyId && m.DocumentNumber == $"VENTA-{saleId}")
            .Select(m => m.Id).ToListAsync();
        var installments = await _db.AccountsReceivables.AsNoTracking()
            .Where(a => a.CompanyId == companyId && movementIds.Contains(a.MovementId))
            .OrderBy(a => a.InstallmentNumber).ToListAsync();
        var ids = installments.Select(i => i.Id).ToArray();
        var payments = await _db.InstallmentPayments.AsNoTracking()
            .Where(p => p.CompanyId == companyId && ids.Contains(p.InstallmentId))
            .OrderBy(p => p.Id).ToListAsync();

        return Ok(new {
            saleId, sale.TotalAmount, sale.AmountReceived,
            balance = sale.TotalAmount - sale.AmountReceived.GetValueOrDefault(),
            installments = installments.Select(i => new {
                i.Id, i.InstallmentNumber, i.InstallmentAmount, i.PaidAmount,
                balance = i.InstallmentAmount - i.PaidAmount,
                i.DueDate, i.Status, i.Frequency,
                payments = payments.Where(p => p.InstallmentId == i.Id)
            }).ToArray()
        });
    }

    [HttpPost]
    public async Task<IActionResult> PostInstallmentPayment([FromBody] RecordPaymentRequest request)
    {
        var companyId = ObtenerEmpresaSegura();
        var userId = ObtenerUsuarioSeguro();
        if (companyId <= 0 || userId <= 0) return Unauthorized();
        if (request.InstallmentId <= 0 || request.PaymentAmount <= 0 ||
            decimal.Round(request.PaymentAmount, 2) != request.PaymentAmount)
            return BadRequest(new { error = "Ingresa una cuota y un abono positivo con hasta 2 decimales." });
        if (request.Observation?.Length > 2000)
            return BadRequest(new { error = "La observación no puede superar 2000 caracteres." });

        var original = await _db.AccountsReceivables.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == request.InstallmentId && a.CompanyId == companyId);
        if (original == null) return NotFound();
        var movement = await _db.Movements.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == original.MovementId && m.CompanyId == companyId);
        if (movement == null || movement.DocumentNumber is null ||
            !movement.DocumentNumber.StartsWith("VENTA-", StringComparison.Ordinal) ||
            !int.TryParse(movement.DocumentNumber.AsSpan("VENTA-".Length), out var saleId))
            return Conflict(new { error = "La cuota no está vinculada a una venta válida." });
        var writable = WritableInventories(companyId, userId);
        if (!await _db.Articles.AnyAsync(a => a.Id == movement.ArticleId && a.CompanyId == companyId &&
            writable.Contains(a.InventoryId))) return Forbid();

        await using var tx = await _db.Database.BeginTransactionAsync();
        // Mismo orden de bloqueo que la anulación: venta primero, cuota después.
        var sale = await _db.Sales.FromSqlInterpolated(
            $"SELECT * FROM \"Sales\" WHERE \"Id\" = {saleId} AND \"CompanyId\" = {companyId} FOR UPDATE")
            .SingleOrDefaultAsync();
        if (sale == null || sale.PaymentType != PaymentType.Cuotas)
            return Conflict(new { error = "La cuota no corresponde a una venta a crédito." });
        if (movement.ActionId != 2 ||
            !await _db.SaleDetails.AnyAsync(d => d.SaleId == sale.Id && d.CompanyId == companyId &&
                d.ArticleId == movement.ArticleId))
            return Conflict(new { error = "El movimiento de la cuota no corresponde al detalle de la venta." });
        if (await _db.SaleCancellations.AnyAsync(c => c.SaleId == saleId && c.CompanyId == companyId))
            return Conflict(new { error = "La venta fue anulada y no admite abonos." });

        var installment = await _db.AccountsReceivables.FromSqlInterpolated(
            $"SELECT * FROM \"AccountsReceivables\" WHERE \"Id\" = {request.InstallmentId} AND \"CompanyId\" = {companyId} FOR UPDATE")
            .SingleOrDefaultAsync();
        if (installment == null || installment.MovementId != movement.Id ||
            installment.Status is "Cancelled" or "Canceled")
            return Conflict(new { error = "La cuota fue cancelada o no está disponible." });

        var recorded = await _db.InstallmentPayments.Where(p => p.InstallmentId == installment.Id &&
            p.CompanyId == companyId).SumAsync(p => (decimal?)p.PaymentAmount) ?? 0m;
        if (recorded != installment.PaidAmount)
            return Conflict(new { error = "Los abonos de esta cuota no cuadran con el saldo; requiere revisión." });

        var balance = installment.InstallmentAmount - installment.PaidAmount;
        if (request.PaymentAmount > balance || balance <= 0)
            return Conflict(new { error = "El abono supera el saldo pendiente de la cuota." });

        var collected = sale.AmountReceived.GetValueOrDefault();
        if (collected + request.PaymentAmount > sale.TotalAmount)
            return Conflict(new { error = "El cobro superaría el total de la venta." });

        var now = DateTime.UtcNow;
        installment.PaidAmount += request.PaymentAmount;
        installment.Status = installment.PaidAmount == installment.InstallmentAmount ? "Paid" : "Pending";
        if (installment.Status == "Paid")
            installment.PaymentDate = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        sale.AmountReceived = collected + request.PaymentAmount;

        var payment = new InstallmentPayment {
            CompanyId = companyId,
            InstallmentId = installment.Id,
            PaymentAmount = request.PaymentAmount,
            PaymentDate = now.ToString("O", CultureInfo.InvariantCulture),
            Observation = request.Observation?.Trim()
        };
        _db.InstallmentPayments.Add(payment);
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return CreatedAtAction(nameof(GetInstallmentPayment), new { id = payment.Id }, new {
            payment.Id, payment.InstallmentId, payment.PaymentAmount, payment.PaymentDate,
            installmentPaid = installment.PaidAmount,
            installmentBalance = installment.InstallmentAmount - installment.PaidAmount,
            saleBalance = sale.TotalAmount - sale.AmountReceived.GetValueOrDefault()
        });
    }

    // Cambiar o borrar abonos directamente desajustaría el saldo.
    // Una reversión de cobro debe implementarse como una operación financiera auditada.
    [HttpPut("{id:int}")]
    public IActionResult PutInstallmentPayment(int id) =>
        Conflict(new { error = "No se permite editar abonos directamente. Registra una reversión auditada." });

    [HttpDelete("{id:int}")]
    public IActionResult DeleteInstallmentPayment(int id) =>
        Conflict(new { error = "No se permite borrar abonos. Registra una reversión auditada." });
}
