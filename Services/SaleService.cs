using InventoryAPI.Models;
using InventoryAPI.Data;
using InventoryAPI.Repositories;
using InventoryAPI.Repositories.IRepositories;
using InventoryAPI.Services.IServices;
using Microsoft.EntityFrameworkCore;

namespace InventoryAPI.Services;

// Una venta y sus movimientos se confirman en una única transacción.
public class SaleService(IWorkFlow workFlow, AppDbContext db)
    : WorkContainer<Sale>(workFlow), ISaleService
{
    public async Task<(bool Success, string Message)> ProcessSaleAsync(Sale sale)
    {
        if (sale.SaleDetails == null || sale.SaleDetails.Count == 0 || sale.CompanyId <= 0 || sale.UserId <= 0)
            return (false, "Venta sin usuario, empresa o artículos.");
        if (sale.SaleDetails.Any(d => d.Quantity <= 0 || d.UnitPrice < 0))
            return (false, "Todos los artículos requieren una cantidad positiva y precio no negativo.");
        if (!await db.Users.AnyAsync(u => u.Id == sale.UserId && (u.CompanyId == sale.CompanyId || u.RoleId == 1) && u.IsActive))
            return (false, "Usuario vendedor no autorizado.");

        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            var summary = sale.SaleDetails.GroupBy(d => d.ArticleId).OrderBy(g => g.Key)
                .Select(g => new { ArticleId = g.Key, Quantity = g.Sum(d => d.Quantity) }).ToArray();
            var locked = new Dictionary<int, Article>();
            foreach (var item in summary)
            {
                var article = await db.Articles.FromSqlInterpolated(
                    $"SELECT * FROM \"Articles\" WHERE \"Id\" = {item.ArticleId} AND \"CompanyId\" = {sale.CompanyId} FOR UPDATE")
                    .SingleOrDefaultAsync();
                if (article == null || !article.IsActive)
                    return (false, $"Artículo #{item.ArticleId} no disponible.");
                if (article.Tracking.ToString() == "Serialized")
                    return (false, "La venta de artículos serializados debe identificar sus series. Utiliza POS con selección de unidades.");
                if (item.Quantity > article.Stock)
                    return (false, $"Stock insuficiente de {article.Name}.");
                if (article.Tracking.ToString() == "Serialized" && item.Quantity != decimal.Truncate(item.Quantity))
                    return (false, "Los artículos serializados se venden por unidades completas.");
                locked[article.Id] = article;
            }

            sale.Id = 0;
            sale.SaleDate = DateTime.UtcNow;
            sale.TotalAmount = sale.SaleDetails.Sum(d => d.Quantity * d.UnitPrice);
            foreach (var d in sale.SaleDetails)
            {
                d.Id = 0; d.SaleId = 0; d.CompanyId = sale.CompanyId;
                d.SubTotal = d.Quantity * d.UnitPrice;
            }
            db.Sales.Add(sale);
            foreach (var item in summary)
            {
                locked[item.ArticleId].Stock -= item.Quantity;
                locked[item.ArticleId].ModificationDate = DateTime.UtcNow;
            }
            await db.SaveChangesAsync();
            foreach (var detail in sale.SaleDetails)
            {
                db.Movements.Add(new Movement
                {
                    ArticleId = detail.ArticleId, CompanyId = sale.CompanyId,
                    ActionId = 2, MovementDate = DateTime.UtcNow, Amount = detail.Quantity,
                    Recipient = string.IsNullOrWhiteSpace(sale.CustomerName) ? "Público general" : sale.CustomerName,
                    PaymentMethod = sale.PaymentType.ToString(), SalePrice = detail.UnitPrice,
                    DocumentNumber = $"VENTA-{sale.Id}", Observation = sale.Notes
                });
            }
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return (true, "Venta confirmada con stock y movimientos consistentes.");
        }
        catch (DbUpdateException)
        {
            return (false, "La venta no pudo registrarse; se revirtió la operación.");
        }
    }
}
