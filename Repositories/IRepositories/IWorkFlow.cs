using ControlInventario.Shared.Models;

namespace InventoryAPI.Repositories.IRepositories
{
    public interface IWorkFlow : IDisposable
    {
        IGenericRepository<TEntity> Repository<TEntity>() where TEntity : class;
        IGenericRepository<AccountReceivable> AccountReceivables { get; }
        IGenericRepository<ActionItem> ActionItems { get; }
        IGenericRepository<Article> Articles { get; }
        IGenericRepository<Brand> Brands { get; }
        IGenericRepository<Category> Categories { get; }
        IGenericRepository<Currency> Currencies { get; }
        IGenericRepository<Customer> Customers { get; }
        IGenericRepository<Employee> Employees { get; }
        IGenericRepository<ExportRoute> ExportRoutes { get; }
        IGenericRepository<ExchangeRate> ExchangeRates { get; }
        IGenericRepository<HistoryLog> HistoryLogs { get; }
        IGenericRepository<InstallmentPayment> InstallmentPayments { get; }
        IGenericRepository<Inventory> Inventories { get; }
        IGenericRepository<Movement> Movements { get; }
        IGenericRepository<Notification> Notifications { get; }
        IGenericRepository<Parameters> Parameters { get; }
        IGenericRepository<Permission> Permissions { get; }
        IGenericRepository<Profile> Profiles { get; }
        IGenericRepository<RecoveryAttempt> RecoveryAttempts { get; }
        IGenericRepository<Role> Roles { get; }
        IGenericRepository<RolePermission> RolePermissions { get; }
        IGenericRepository<Sale> Sales { get; }
        IGenericRepository<SalesMode> SalesModes { get; }
        IGenericRepository<SecurityQuestion> SecurityQuestions { get; }
        IGenericRepository<Supplier> Suppliers { get; }
        IGenericRepository<Theme> Themes { get; }
        IGenericRepository<TimeZoneItem> TimeZones { get; }
        IGenericRepository<User> Users { get; }
        IGenericRepository<CategoryMeasurementUnit> CategoryMeasurementUnits { get; }

        Task<int> CompleteAsync();
        Task BeginTransactionAsync();
        Task CommitTransactionAsync();
        Task RollbackTransactionAsync();
    }
}