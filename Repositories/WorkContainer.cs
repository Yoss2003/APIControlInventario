using InventoryAPI.Repositories.IRepositories;
using System.Linq.Expressions;

namespace InventoryAPI.Repositories
{
    public class WorkContainer<T>(IWorkFlow workFlow) : IWorkContainer<T> where T : class
    {
        protected readonly IWorkFlow _workFlow = workFlow;

        public virtual async Task<IEnumerable<T>> GetAllAsync()
        {
            return await _workFlow.Repository<T>().GetAllAsync();
        }

        public virtual async Task<T?> GetByIdAsync(int id)
        {
            return await _workFlow.Repository<T>().GetByIdAsync(id);
        }

        public virtual async Task<bool> CreateAsync(T entity)
        {
            await _workFlow.Repository<T>().AddAsync(entity);
            var result = await _workFlow.CompleteAsync();
            return result > 0;
        }

        public virtual async Task<bool> UpdateAsync(T entity)
        {
            _workFlow.Repository<T>().Update(entity);
            var result = await _workFlow.CompleteAsync();
            return result > 0;
        }

        public virtual async Task<bool> DeleteAsync(int id, string deletedBy = "Sistema")
        {
            var entity = await _workFlow.Repository<T>().GetByIdAsync(id);
            if (entity == null) return false;

            if (entity is ControlInventario.Shared.Models.Interfaces.ISoftDelete softDeleteEntity)
            {
                softDeleteEntity.IsActive = false;
                softDeleteEntity.DeletionDate = DateTime.Now;
                softDeleteEntity.DeletionUser = deletedBy;

                _workFlow.Repository<T>().Update(entity);
            }
            else
            {
                _workFlow.Repository<T>().Delete(entity);
            }

            var result = await _workFlow.CompleteAsync();
            return result > 0;
        }

        public virtual async Task<IEnumerable<T>> GetAllByCompanyIdAsync(int companyId)
        {
            var propertyInfo = typeof(T).GetProperty("CompanyId") ?? throw new InvalidOperationException($"La entidad {typeof(T).Name} no tiene una columna 'CompanyId'. No puedes filtrar esto por sucursal.");
            var parameter = Expression.Parameter(typeof(T), "x");
            var property = Expression.Property(parameter, propertyInfo);
            var constant = Expression.Constant(companyId);
            var equals = Expression.Equal(property, constant);
            var lambda = Expression.Lambda<Func<T, bool>>(equals, parameter);

            return await _workFlow.Repository<T>().FindAsync(lambda);
        }
    }
}