using InventoryAPI.Models;
using InventoryAPI.Data;
using InventoryAPI.Repositories;
using InventoryAPI.Repositories.IRepositories;
using InventoryAPI.Services.IServices;
using Microsoft.EntityFrameworkCore;

namespace InventoryAPI.Services;

// Venta, movimientos, stock y cronograma se confirman en una misma transacción.
public class SaleService(IWorkFlow workFlow, AppDbContext db)
    : WorkContainer<Sale>(workFlow), ISaleService
{
    public async Task<(bool Success, string Message)> ProcessSaleAsync(Sale sale)
    {
        if (sale.SaleDetails == null || sale.SaleDetails.Count == 0 || sale.CompanyId <= 0 || sale.UserId <= 0)
            return (false, "Venta sin usuario, empresa o artículos.");
        if (sale.SaleDetails.Any(d => d.ArticleId <= 0 || d.Quantity <= 0 || d.UnitPrice < 0))
            return (false, "Todos los artículos requieren identificador, cantidad positiva y precio no negativo.");
        if (!Enum.IsDefined(sale.PaymentType))
            return (false, "Método de pago desconocido.");
        if (!await db.Users.AnyAsync(u => u.Id == sale.UserId &&
            (u.CompanyId == sale.CompanyId || u.RoleId == 1) && u.IsActive))
            return (false, "Usuario vendedor no autorizado.");

        // No confiar en los importes enviados por el navegador o MAUI.
        foreach (var d in sale.SaleDetails)
            d.SubTotal = decimal.Round(d.Quantity * d.UnitPrice, 2, MidpointRounding.AwayFromZero);
        var total = sale.SaleDetails.Sum(d => d.SubTotal);
        if (total <= 0)
            return (false, "La venta debe tener un importe mayor a cero.");

        List<SaleInstallment> schedule = [];
        if (sale.PaymentType == PaymentType.Cuotas)
        {
            if (!SaleInstallmentCalculator.TryBuild(total, sale.InstallmentPlan, SaleInstallmentCalculator.BusinessToday(),
                out schedule, out var error))
                return (false, error);
            sale.AmountReceived = sale.InstallmentPlan!.DownPayment;
            sale.ChangeGiven = 0m;
        }
        else if (sale.InstallmentPlan != null)
            return (false, "El cronograma solo se puede configurar para ventas a cuotas.");
        else if (sale.AmountReceived < 0 || sale.ChangeGiven < 0)
            return (false, "Los importes recibidos y de vuelto no pueden ser negativos.");

        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            if (sale.SalesModeId <= 0)
            {
                var modeName = sale.PaymentType == PaymentType.Cuotas ? schedule[0].Frequency : "Venta mostrador";
                var mode = await db.SalesModes.FirstOrDefaultAsync(m => m.SalesModeName == modeName);
                if (mode == null)
                {
                    mode = new SalesMode { SalesModeName = modeName };
                    db.SalesModes.Add(mode);
                    await db.SaveChangesAsync();
                }
                sale.SalesModeId = mode.Id;
            }
            else if (!await db.SalesModes.AnyAsync(m => m.Id == sale.SalesModeId))
                return (false, "La modalidad de venta indicada no existe.");

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
                locked[article.Id] = article;
            }

            sale.Id = 0;
            sale.SaleDate = DateTime.UtcNow;
            sale.TotalAmount = total;
            foreach (var detail in sale.SaleDetails)
            {
                detail.Id = 0;
                detail.SaleId = 0;
                detail.CompanyId = sale.CompanyId;
            }
            db.Sales.Add(sale);
            foreach (var item in summary)
            {
                locked[item.ArticleId].Stock -= item.Quantity;
                locked[item.ArticleId].ModificationDate = DateTime.UtcNow;
            }
            await db.SaveChangesAsync();

            foreach (var item in schedule)
            {
                item.SaleId = sale.Id;
                item.CompanyId = sale.CompanyId;
            }
            db.SaleInstallments.AddRange(schedule);

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
            return (true, "Venta, stock, movimientos y cronograma confirmados.");
        }
        catch (DbUpdateException)
        {
            return (false, "La venta no pudo registrarse; se revirtió la operación.");
        }
    }
}
