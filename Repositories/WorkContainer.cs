using InventoryAPI.Repositories.IRepositories;

namespace InventoryAPI.Repositories
{
    public class WorkContainer<T> : IWorkContainer<T> where T : class
    {
        protected readonly IWorkFlow _workFlow;

        public WorkContainer(IWorkFlow workFlow)
        {
            _workFlow = workFlow;
        }

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

        public virtual async Task<bool> DeleteAsync(int id)
        {
            var entity = await _workFlow.Repository<T>().GetByIdAsync(id);
            if (entity == null) return false;

            _workFlow.Repository<T>().Delete(entity);
            var result = await _workFlow.CompleteAsync();
            return result > 0;
        }
    }
}