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

            await _workFlow.BeginTransactionAsync();
            try
            {
                categoriaExistente.Name = category.Name;
                categoriaExistente.Description = category.Description;
                categoriaExistente.ParentCategoryId = category.ParentCategoryId;
                categoriaExistente.TrackingMode = category.TrackingMode;
                categoriaExistente.NamingMethod = category.NamingMethod;
                categoriaExistente.IsReturnable = category.IsReturnable;

                categoriaExistente.IsActive = category.IsActive;
                categoriaExistente.Label1 = category.Label1;
                categoriaExistente.Label2 = category.Label2;
                categoriaExistente.Label3 = category.Label3;
                categoriaExistente.Label4 = category.Label4;
                categoriaExistente.Label5 = category.Label5;
                categoriaExistente.Label6 = category.Label6;

                categoriaExistente.ModificationDate = DateTime.Now;
                categoriaExistente.ModificationUser = category.ModificationUser ?? "Admin";

                _workFlow.Repository<Category>().Update(categoriaExistente);

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
                .FindAsync(c => c.ParentCategoryId == id && c.IsActive);
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

            category.IsActive = false;
            category.DeletionDate = DateTime.Now;

            _workFlow.Repository<Category>().Update(category);
            await _workFlow.CompleteAsync();

            return (true, "Categoría eliminada con éxito.");
        }
    }
}