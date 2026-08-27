using ControlInventario.Shared.Models;
using InventoryAPI.Repositories;
using InventoryAPI.Repositories.IRepositories;
using InventoryAPI.Services.IServices;

namespace InventoryAPI.Services
{
    public class ArticleService(IWorkFlow workFlow) : WorkContainer<Article>(workFlow), IArticleService
    {
        public override async Task<bool> CreateAsync(Article article)
        {
            await _workFlow.BeginTransactionAsync();

            try
            {
                await _workFlow.Repository<Article>().AddAsync(article);
                await _workFlow.CompleteAsync();

                var nuevoMovimiento = new Movement
                {
                    ArticleId = article.Id,
                    EmployeeId = article.LoggedUserId,
                    ActionId = 1,
                    MovementDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    Observation = "Registro inicial del producto en almacén",
                    Amount = (double)article.Stock,
                    SalePrice = (double)(article.SalePrice ?? 0m),
                    CompanyId = article.CompanyId
                };
                await _workFlow.Repository<Movement>().AddAsync(nuevoMovimiento);

                string nombreUsuario = string.IsNullOrWhiteSpace(article.LoggedUserFullName)
                                       ? "Usuario Desconocido"
                                       : article.LoggedUserFullName;

                var nuevoLog = new HistoryLog
                {
                    LogDate = DateTime.Now,
                    Username = nombreUsuario,
                    ModuleName = "Inventario",
                    ActionName = "Creación",
                    Detail = $"Producto \"{article.Name}\" agregado por \"{nombreUsuario}\" el \"{DateTime.Now:dd/MM/yyyy HH:mm}\"",
                    CompanyId = article.CompanyId
                };
                await _workFlow.Repository<HistoryLog>().AddAsync(nuevoLog);

                await _workFlow.CompleteAsync();
                await _workFlow.CommitTransactionAsync();

                return true;
            }
            catch (Exception)
            {
                await _workFlow.RollbackTransactionAsync();
                throw;
            }
        }

        public async Task<int> GetArticleCountByInventoryIdAsync(int inventoryId)
        {
            var articles = await _workFlow.Repository<Article>().FindAsync(a => a.InventoryId == inventoryId);
            return (int)articles.Sum(a => a.Stock);
        }

        public async Task<Article?> GetArticleByBarcodeAsync(string barcode)
        {
            var articles = await _workFlow.Repository<Article>().FindAsync(a => a.Barcode == barcode);
            return articles.FirstOrDefault();
        }
    }
}