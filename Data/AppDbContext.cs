using Microsoft.EntityFrameworkCore;
using InventoryAPI.Models;

namespace InventoryAPI.Data
{
    public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
    {
        public DbSet<Inventory> Inventories { get; set; }
        public DbSet<Parameters> Parameters { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Brand> Brands { get; set; }
        public DbSet<Article> Articles { get; set; }
        public DbSet<Supplier> Suppliers { get; set; }
        public DbSet<ActionItem> Actions { get; set; }
        public DbSet<Language> Languages { get; set; }
        public DbSet<Theme> Themes { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<TimeZoneItem> TimeZones { get; set; }
        public DbSet<Currency> Currencies { get; set; }
        public DbSet<DateFormat> DateFormats { get; set; }
        public DbSet<MeasurementUnit> MeasurementUnits { get; set; }
        public DbSet<SalesMode> SalesModes { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<Permission> Permissions { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }
        public DbSet<Employee> Employees { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Profile> Profiles { get; set; }
        public DbSet<Company> Companies { get; set; }
        public DbSet<Movement> Movements { get; set; }
        public DbSet<AccountReceivable> AccountsReceivables { get; set; }
        public DbSet<InstallmentPayment> InstallmentPayments { get; set; }
        public DbSet<Sale> Sales { get; set; }
        public DbSet<SaleDetail> SaleDetails { get; set; }
        public DbSet<SecurityQuestion> SecurityQuestions { get; set; }
        public DbSet<RecoveryAttempt> RecoveryAttempts { get; set; }
        public DbSet<HistoryLog> HistoryLogs { get; set; }
        public DbSet<ExportRoute> ExportRoutes { get; set; }
        public DbSet<ExchangeRate> ExchangeRates { get; set; }
        public DbSet<EmployeePermission> EmployeePermissions { get; set; }
        public DbSet<SharedInventory> SharedInventories { get; set; }
        public DbSet<CategoryMeasurementUnit> CategoryMeasurementUnits { get; set; }
        public DbSet<ArticleUnitOperation> ArticleUnitOperations { get; set; }
        public DbSet<CompanyLicense> CompanyLicenses { get; set; }
        public DbSet<PasswordRecoveryRequest> PasswordRecoveryRequests { get; set; }
        public DbSet<ArticleAssignment> ArticleAssignments { get; set; }
        public DbSet<ArticleAssignmentEvent> ArticleAssignmentEvents { get; set; }
        public DbSet<MovementCancellation> MovementCancellations { get; set; }
        public DbSet<SaleOperation> SaleOperations { get; set; }
        public DbSet<SaleCancellation> SaleCancellations { get; set; }
        public DbSet<CompanyLogo> CompanyLogos { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            // Nunca puede haber dos inventarios maestros para una misma empresa.
            modelBuilder.Entity<Inventory>(e => {
                e.HasIndex(x => x.CompanyId)
                    .IsUnique()
                    .HasDatabaseName("IX_Inventories_CompanyId_Master")
                    .HasFilter("\"IsMaster\" = true");
            });
            modelBuilder.Entity<Category>(e => {
                e.HasIndex(x => new { x.CompanyId, x.NameKey }).IsUnique()
                    .HasDatabaseName("IX_Categories_MasterRoot_NameKey")
                    .HasFilter("\"ParentCategoryId\" IS NULL AND \"IsActive\" = true");
                e.HasIndex(x => new { x.CompanyId, x.ParentCategoryId, x.NameKey }).IsUnique()
                    .HasDatabaseName("IX_Categories_MasterChild_NameKey")
                    .HasFilter("\"ParentCategoryId\" IS NOT NULL AND \"IsActive\" = true");
            });
            modelBuilder.Entity<Supplier>(e => {
                e.HasIndex(x => new { x.CompanyId, x.Ruc }).IsUnique()
                    .HasDatabaseName("IX_Suppliers_Company_Ruc")
                    .HasFilter("\"IsActive\" = true");
            });
            modelBuilder.Entity<Brand>(e => {
                e.HasIndex(x => new { x.CompanyId, x.CategoryId, x.NameKey }).IsUnique()
                    .HasDatabaseName("IX_Brands_Company_Category_NameKey")
                    .HasFilter("\"IsActive\" = true");
            });
            modelBuilder.Entity<ArticleUnitOperation>(e => {
                e.HasIndex(x => x.ArticleDetailId).IsUnique().HasFilter("\"IsReversed\" = false");
                e.HasIndex(x => new { x.CompanyId, x.ArticleId, x.SaleId });
                e.HasIndex(x => new { x.CompanyId, x.ArticleId, x.AssignmentId });
                e.HasOne<ArticleDetails>().WithMany().HasForeignKey(x => x.ArticleDetailId).OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<CompanyLicense>(e => {
                e.HasIndex(x => x.CompanyId).IsUnique();
                e.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<PasswordRecoveryRequest>(e => {
                e.HasIndex(x => x.TokenHash).IsUnique();
                e.HasIndex(x => new { x.UserId, x.RequestedAt });
                e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<AccountReceivable>()
                .Property(a => a.InstallmentAmount).HasColumnType("decimal(18,2)");

            modelBuilder.Entity<ArticleAssignment>(e =>
            {
                e.HasIndex(x => x.OperationKey).IsUnique();
                e.HasIndex(x => new { x.CompanyId, x.InventoryId, x.ArticleId });
                e.HasIndex(x => new { x.CompanyId, x.RecipientUserId });
                e.HasOne<Inventory>().WithMany().HasForeignKey(x => x.InventoryId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne<Article>().WithMany().HasForeignKey(x => x.ArticleId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne<User>().WithMany().HasForeignKey(x => x.RecipientUserId).OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<SaleOperation>().HasIndex(x => x.OperationKey).IsUnique();
            modelBuilder.Entity<SaleCancellation>().HasIndex(x => x.SaleId).IsUnique();
            modelBuilder.Entity<MovementCancellation>(e =>
            {
                e.HasIndex(x => x.OriginalMovementId).IsUnique();
                e.HasIndex(x => x.ReversalMovementId).IsUnique();
            });
            modelBuilder.Entity<ArticleAssignmentEvent>(e =>
            {
                e.HasIndex(x => x.OperationKey).IsUnique();
                e.HasOne(x => x.Assignment).WithMany(x => x.Events)
                    .HasForeignKey(x => x.AssignmentId).OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<CompanyLogo>(e => {
                e.HasKey(x => x.CompanyId);
                e.Property(x => x.ContentType).HasMaxLength(32).IsRequired();
                e.Property(x => x.Content).IsRequired();
                e.HasOne<Company>().WithOne()
                    .HasForeignKey<CompanyLogo>(x => x.CompanyId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

        }
    }
}
