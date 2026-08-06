using ControlInventario.Shared.Models;
using InventoryAPI.Data;
using InventoryAPI.Repositories.IRepositories;
using Microsoft.EntityFrameworkCore.Storage;

namespace InventoryAPI.Repositories
{
    public class WorkFlow : IWorkFlow
    {
        private readonly AppDbContext _context;
        private IDbContextTransaction? _transaction;
        public IGenericRepository<AccountReceivable> AccountReceivables { get; private set; }
        public IGenericRepository<ActionItem> ActionItems { get; private set; }
        public IGenericRepository<Article> Articles { get; private set; }
        public IGenericRepository<Brand> Brands { get; private set; }
        public IGenericRepository<Category> Categories { get; private set; }
        public IGenericRepository<Currency> Currencies { get; private set; }
        public IGenericRepository<Customer> Customers { get; private set; }
        public IGenericRepository<Employee> Employees { get; private set; }
        public IGenericRepository<ExportRoute> ExportRoutes { get; private set; }
        public IGenericRepository<ExchangeRate> ExchangeRates { get; private set; }
        public IGenericRepository<HistoryLog> HistoryLogs { get; private set; }
        public IGenericRepository<InstallmentPayment> InstallmentPayments { get; private set; }
        public IGenericRepository<Inventory> Inventories { get; private set; }
        public IGenericRepository<Language> Languages { get; private set; }
        public IGenericRepository<MeasurementUnit> MeasurementUnits { get; private set; }
        public IGenericRepository<Movement> Movements { get; private set; }
        public IGenericRepository<Notification> Notifications { get; private set; }
        public IGenericRepository<Parameters> Parameters { get; private set; }
        public IGenericRepository<Permission> Permissions { get; private set; }
        public IGenericRepository<Profile> Profiles { get; private set; }
        public IGenericRepository<RecoveryAttempt> RecoveryAttempts { get; private set; }
        public IGenericRepository<Role> Roles { get; private set; }
        public IGenericRepository<RolePermission> RolePermissions { get; private set; }
        public IGenericRepository<Sale> Sales { get; private set; }
        public IGenericRepository<SalesMode> SalesModes { get; private set; }
        public IGenericRepository<SecurityQuestion> SecurityQuestions { get; private set; }
        public IGenericRepository<Supplier> Suppliers { get; private set; }
        public IGenericRepository<Theme> Themes { get; private set; }
        public IGenericRepository<TimeZoneItem> TimeZones { get; private set; }
        public IGenericRepository<User> Users { get; private set; }
        public IGenericRepository<CategoryMeasurementUnit> CategoryMeasurementUnits { get; private set; }


        public WorkFlow(AppDbContext context)
        {
            _context = context;
            AccountReceivables = new GenericRepository<AccountReceivable>(_context);
            ActionItems = new GenericRepository<ActionItem>(_context);
            Articles = new GenericRepository<Article>(_context);
            Brands = new GenericRepository<Brand>(_context);
            Categories = new GenericRepository<Category>(_context);
            Currencies = new GenericRepository<Currency>(_context);
            Customers = new GenericRepository<Customer>(_context);
            Employees = new GenericRepository<Employee>(_context);
            ExportRoutes = new GenericRepository<ExportRoute>(_context);
            ExchangeRates = new GenericRepository<ExchangeRate>(_context);
            HistoryLogs = new GenericRepository<HistoryLog>(_context);
            InstallmentPayments = new GenericRepository<InstallmentPayment>(_context);
            Inventories = new GenericRepository<Inventory>(_context);
            Languages = new GenericRepository<Language>(_context);
            MeasurementUnits = new GenericRepository<MeasurementUnit>(_context);
            Movements = new GenericRepository<Movement>(_context);
            Notifications = new GenericRepository<Notification>(_context);
            Parameters = new GenericRepository<Parameters>(_context);
            Permissions = new GenericRepository<Permission>(_context);
            Profiles = new GenericRepository<Profile>(_context);
            RecoveryAttempts = new GenericRepository<RecoveryAttempt>(_context);
            Roles = new GenericRepository<Role>(_context);
            RolePermissions = new GenericRepository<RolePermission>(_context);
            Sales = new GenericRepository<Sale>(_context);
            SalesModes = new GenericRepository<SalesMode>(_context);
            SecurityQuestions = new GenericRepository<SecurityQuestion>(_context);
            Suppliers = new GenericRepository<Supplier>(_context);
            Themes = new GenericRepository<Theme>(_context);
            TimeZones = new GenericRepository<TimeZoneItem>(_context);
            Users = new GenericRepository<User>(_context);
            CategoryMeasurementUnits = new GenericRepository<CategoryMeasurementUnit>(_context);
        }

        public IGenericRepository<TEntity> Repository<TEntity>() where TEntity : class
        {
            return new GenericRepository<TEntity>(_context);
        }

        public async Task<int> CompleteAsync()
        {
            return await _context.SaveChangesAsync();
        }

        public async Task BeginTransactionAsync()
        {
            _transaction = await _context.Database.BeginTransactionAsync();
        }

        public async Task CommitTransactionAsync()
        {
            if (_transaction != null)
            {
                await _transaction.CommitAsync();
                await _transaction.DisposeAsync();
            }
        }

        public async Task RollbackTransactionAsync()
        {
            if (_transaction != null)
            {
                await _transaction.RollbackAsync();
                await _transaction.DisposeAsync();
            }
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}