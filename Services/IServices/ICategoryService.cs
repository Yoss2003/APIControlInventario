using ControlInventario.Shared.Models;
using InventoryAPI.Repositories.IRepositories;

namespace InventoryAPI.Services.IServices
{
    public interface ICategoryService : IWorkContainer<Category>
    {
        Task<(bool Success, string Message)> CreateCategoryAsync(Category category);
        Task<(bool Success, string Message)> UpdateCategoryAsync(int id, Category category);
        Task<(bool Success, string Message)> DeleteCategoryAsync(int id);
    }
}