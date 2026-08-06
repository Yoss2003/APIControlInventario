using ControlInventario.Shared.Models;
using InventoryAPI.Repositories.IRepositories;

namespace InventoryAPI.Services.IServices
{
    public interface IArticleService : IWorkContainer<Article>
    {
        Task<int> GetArticleCountByInventoryIdAsync(int inventoryId);
        Task<Article?> GetArticleByBarcodeAsync(string barcode);
    }
}