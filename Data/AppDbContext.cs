using Microsoft.EntityFrameworkCore;
using InventoryAPI.Models;

namespace InventoryAPI.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // 1. Core e Inventario
        public DbSet<Inventory> Inventories { get; set; }
        public DbSet<Parameter> Parameters { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Brand> Brands { get; set; }
        public DbSet<Article> Articles { get; set; }
        public DbSet<RegistrationGroup> RegistrationGroups { get; set; }
        public DbSet<Supplier> Suppliers { get; set; }

        // 2. Configuración y Catálogos (Tablas Maestras)
        public DbSet<ActionItem> Actions { get; set; }
        public DbSet<Language> Languages { get; set; }
        public DbSet<Theme> Themes { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<TimeZoneItem> TimeZones { get; set; }
        public DbSet<Currency> Currencies { get; set; }
        public DbSet<DateFormat> DateFormats { get; set; }
        public DbSet<MeasurementUnit> MeasurementUnits { get; set; }
        public DbSet<SalesMode> SalesModes { get; set; }

        // 3. Usuarios y Personal
        public DbSet<User> Users { get; set; }
        public DbSet<Employee> Employees { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Profile> Profiles { get; set; }

        // 4. Transacciones y Finanzas
        public DbSet<Movement> Movements { get; set; }
        public DbSet<AccountReceivable> AccountsReceivables { get; set; }
        public DbSet<InstallmentPayment> InstallmentPayments { get; set; }

        // 5. Seguridad y Logs
        public DbSet<SecurityQuestion> SecurityQuestions { get; set; }
        public DbSet<RecoveryAttempt> RecoveryAttempts { get; set; }
        public DbSet<HistoryLog> HistoryLogs { get; set; }
        public DbSet<ExportRoute> ExportRoutes { get; set; }
    }
}
