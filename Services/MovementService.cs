using InventoryAPI.Models;
using InventoryAPI.Data;
using InventoryAPI.Repositories;
using InventoryAPI.Repositories.IRepositories;
using InventoryAPI.Services.IServices;
using Microsoft.EntityFrameworkCore;

namespace InventoryAPI.Services;

// Entrada/salida ordinaria: un movimiento confirmado SIEMPRE altera stock.
// La custodia se registra exclusivamente por AssignmentsController.
public class MovementService(IWorkFlow workFlow, AppDbContext db)
    : WorkContainer<Movement>(workFlow), IMovementService
{
    public override async Task<bool> CreateAsync(Movement movement)
    {
        if (movement.CompanyId <= 0 || movement.ArticleId <= 0 ||
            movement.Amount is null or <= 0)
            throw new ArgumentException("Artículo, empresa y cantidad positiva son obligatorios.");
        if (movement.ActionId is not (1 or 2))
            throw new ArgumentException("Para asignaciones usa /api/Assignments; solo entradas (1) y salidas (2) son admitidas aquí.");

        await using var transaction = await db.Database.BeginTransactionAsync();
        var article = await db.Articles.FromSqlInterpolated(
            $"SELECT * FROM \"Articles\" WHERE \"Id\" = {movement.ArticleId} AND \"CompanyId\" = {movement.CompanyId} FOR UPDATE")
            .SingleOrDefaultAsync();
        if (article is null || !article.IsActive)
            throw new InvalidOperationException("Artículo inexistente o inactivo.");
        if (article.Tracking.ToString() == "Serialized" && movement.Amount != decimal.Truncate(movement.Amount.Value))
            throw new ArgumentException("Un artículo serializado requiere cantidades enteras.");

        if (article.Tracking.ToString() == "Serialized")
            throw new InvalidOperationException("Para serializados, registra primero cada unidad y usa venta/asignación con su ID; no se permite ajuste por cantidad.");
        var delta = movement.ActionId == 1 ? movement.Amount.Value : -movement.Amount.Value;
        if (article.Stock + delta < 0)
            throw new InvalidOperationException("Stock insuficiente para completar la operación.");
        article.Stock += delta;
        article.ModificationDate = DateTime.UtcNow;
        movement.Id = 0;
        movement.MovementDate = DateTime.UtcNow;
        db.Movements.Add(movement);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return true;
    }

    public async Task<(bool Success, string Error)> CancelStockMovementAsync(
        int originalId, int companyId, int userId, string reason)
    {
        if (userId <= 0 || companyId <= 0 || string.IsNullOrWhiteSpace(reason))
            return (false, "Indica el motivo de anulación.");
        await using var transaction = await db.Database.BeginTransactionAsync();
        var movement = await db.Movements.FromSqlInterpolated(
            $"SELECT * FROM \"Movements\" WHERE \"Id\" = {originalId} AND \"CompanyId\" = {companyId} FOR UPDATE")
            .SingleOrDefaultAsync();
        if (movement == null) return (false, "Movimiento inexistente.");
        if (movement.ActionId is not (1 or 2))
            return (false, "Las asignaciones se revierten mediante devolución, no anulación.");
        if (!string.IsNullOrWhiteSpace(movement.PaymentMethod) && movement.PaymentMethod != "N/A")
            return (false, "Las ventas se anulan desde el módulo de ventas, no como movimientos aislados.");
        if (movement.Observation?.Contains("Registro inicial del producto", StringComparison.OrdinalIgnoreCase) == true)
            return (false, "El alta inicial no se cancela con este mecanismo.");
        if (await db.MovementCancellations.AnyAsync(c => c.OriginalMovementId == originalId))
            return (false, "Este movimiento ya fue anulado.");
        var article = await db.Articles.FromSqlInterpolated(
            $"SELECT * FROM \"Articles\" WHERE \"Id\" = {movement.ArticleId} AND \"CompanyId\" = {companyId} FOR UPDATE")
            .SingleOrDefaultAsync();
        if (article == null) return (false, "Artículo original inexistente.");
        var delta = movement.ActionId == 1 ? -(movement.Amount ?? 0m) : (movement.Amount ?? 0m);
        if (article.Stock + delta < 0) return (false, "No se puede anular porque consumiría stock ya utilizado.");
        article.Stock += delta;
        article.ModificationDate = DateTime.UtcNow;
        var reversed = new Movement
        {
            ArticleId = movement.ArticleId, CompanyId = companyId,
            ActionId = movement.ActionId == 1 ? 2 : 1,
            Amount = Math.Abs(delta), MovementDate = DateTime.UtcNow,
            Recipient = movement.Recipient,
            Observation = $"REVERSIÓN MOVIMIENTO #{originalId}: {reason.Trim()}"
        };
        db.Movements.Add(reversed);
        await db.SaveChangesAsync();
        db.MovementCancellations.Add(new MovementCancellation
        {
            OriginalMovementId = originalId, ReversalMovementId = reversed.Id,
            CompanyId = companyId, CancelledByUserId = userId,
            CancelledAt = DateTime.UtcNow, Reason = reason.Trim()
        });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return (true, string.Empty);
    }
}
