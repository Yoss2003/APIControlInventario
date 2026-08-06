using ControlInventario.Shared.Models;
using InventoryAPI.Repositories;
using InventoryAPI.Repositories.IRepositories;
using InventoryAPI.Services.IServices;

namespace InventoryAPI.Services
{
    public class CategoryService(IWorkFlow workFlow) : WorkContainer<Category>(workFlow), ICategoryService
    {
        public override async Task<IEnumerable<Category>> GetAllAsync()
        {
            var categories = await _workFlow.Repository<Category>()
                .GetAllWithIncludeAsync(c => c.CategoryMeasurementUnits!);

            foreach (var cat in categories)
            {
                if (cat.CategoryMeasurementUnits != null)
                {
                    cat.SelectedUnitIds = cat.CategoryMeasurementUnits.Select(cmu => cmu.MeasurementUnitId).ToList();
                }
            }

            return categories;
        }

        public async Task<(bool Success, string Message)> CreateCategoryAsync(Category category)
        {
            await _workFlow.BeginTransactionAsync();
            try
            {
                category.CategoryMeasurementUnits = null;

                await _workFlow.Repository<Category>().AddAsync(category);
                await _workFlow.CompleteAsync();

                if (category.SelectedUnitIds != null && category.SelectedUnitIds.Any())
                {
                    foreach (var unitId in category.SelectedUnitIds)
                    {
                        var newUnit = new CategoryMeasurementUnit
                        {
                            CategoryId = category.Id,
                            MeasurementUnitId = unitId
                        };
                        await _workFlow.Repository<CategoryMeasurementUnit>().AddAsync(newUnit);
                    }
                    await _workFlow.CompleteAsync();
                }

                await _workFlow.CommitTransactionAsync();
                return (true, "Categoría creada con éxito.");
            }
            catch (Exception ex)
            {
                await _workFlow.RollbackTransactionAsync();
                return (false, $"Error crítico al guardar en BD: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message)> UpdateCategoryAsync(int id, Category category)
        {
            var categoriaExistente = await _workFlow.Repository<Category>().GetByIdAsync(id);
            if (categoriaExistente == null) return (false, "Categoría no encontrada.");

            _workFlow.Repository<Category>().Detach(categoriaExistente);

            await _workFlow.BeginTransactionAsync();
            try
            {
                _workFlow.Repository<Category>().Update(category);

                if (category.SelectedUnitIds != null)
                {
                    var unidadesViejas = await _workFlow.Repository<CategoryMeasurementUnit>()
                        .FindAsync(cmu => cmu.CategoryId == id);

                    foreach (var vieja in unidadesViejas)
                    {
                        _workFlow.Repository<CategoryMeasurementUnit>().Delete(vieja);
                    }

                    foreach (var unitId in category.SelectedUnitIds)
                    {
                        var nuevaUnidad = new CategoryMeasurementUnit
                        {
                            CategoryId = id,
                            MeasurementUnitId = unitId
                        };
                        await _workFlow.Repository<CategoryMeasurementUnit>().AddAsync(nuevaUnidad);
                    }
                }

                await _workFlow.CompleteAsync();
                await _workFlow.CommitTransactionAsync();
                return (true, "Categoría actualizada con éxito.");
            }
            catch (Exception ex)
            {
                await _workFlow.RollbackTransactionAsync();
                return (false, $"Error crítico al actualizar en BD: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message)> DeleteCategoryAsync(int id)
        {
            var category = await _workFlow.Repository<Category>().GetByIdAsync(id);
            if (category == null) return (false, "Categoría no encontrada.");

            var subcategories = await _workFlow.Repository<Category>()
                .FindAsync(c => c.ParentCategoryId == id);
            if (subcategories.Any())
            {
                return (false, "No puedes eliminar una categoría padre que aún contiene subcategorías.");
            }

            var articles = await _workFlow.Repository<Article>()
                .FindAsync(a => a.CategoryId == id);
            if (articles.Any())
            {
                return (false, "No puedes eliminar esta categoría porque existen artículos registrados en ella.");
            }

            _workFlow.Repository<Category>().Delete(category);
            await _workFlow.CompleteAsync();

            return (true, "Categoría eliminada con éxito.");
        }
    }
}