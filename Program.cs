using InventoryAPI.Models;
using InventoryAPI.Data;
using InventoryAPI.Repositories;
using InventoryAPI.Repositories.IRepositories;
using InventoryAPI.Services;
using InventoryAPI.Services.IServices;
using InventoryAPI.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

// Tareas en segundo plano (Cron Jobs)
builder.Services.AddHostedService<ApprovalCleanupService>();

// Soporte para clientes HTTP (Necesario para consultar SUNAT)
builder.Services.AddHttpClient();
builder.Services.AddHttpClient<PrivateMediaStorage>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(45);
});
builder.Services.AddScoped<LegacyMediaMigrator>();


// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Registro de WorkFlow
builder.Services.AddScoped<IWorkFlow, WorkFlow>();

// Registro de TODOS los Servicios
builder.Services.AddScoped<IAccountReceivableService, AccountReceivableService>();
builder.Services.AddScoped<IActionItemService, ActionItemService>();
builder.Services.AddScoped<IArticleService, ArticleService>();
builder.Services.AddScoped<IBrandService, BrandService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<ICurrencyService, CurrencyService>();
builder.Services.AddHttpClient<ICustomerService, CustomerService>(client =>
    client.Timeout = TimeSpan.FromSeconds(20));
builder.Services.AddScoped<IDateFormatService, DateFormatService>();
builder.Services.AddScoped<IEmployeeService, EmployeeService>();
builder.Services.AddScoped<IExportRouteService, ExportRouteService>();

builder.Services.AddHttpClient<IExchangeRateService, ExchangeRateService>();
builder.Services.AddHttpClient<ISupplierService, SupplierService>();

builder.Services.AddScoped<IHistoryLogService, HistoryLogService>();
builder.Services.AddScoped<IInstallmentPaymentService, InstallmentPaymentService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<ILanguageService, LanguageService>();
builder.Services.AddScoped<IMeasurementUnitService, MeasurementUnitService>();
builder.Services.AddScoped<IMovementService, MovementService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IParametersService, ParametersService>();
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<IProfileService, ProfileService>();
builder.Services.AddScoped<IRecoveryAttemptService, RecoveryAttemptService>();
builder.Services.AddScoped<IRoleService, RoleService>();
builder.Services.AddScoped<ISaleService, SaleService>();
builder.Services.AddScoped<ISalesModeService, SalesModeService>();
builder.Services.AddScoped<ISecurityQuestionService, SecurityQuestionService>();
builder.Services.AddScoped<IThemeService, ThemeService>();
builder.Services.AddScoped<ITimeZoneItemService, TimeZoneItemService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ICompanyLicenseService, CompanyLicenseService>();
builder.Services.AddScoped<InventoryAccessPolicy>();
builder.Services.AddScoped<ManagedUserRegistration>();

builder.Services.AddScoped<IRolePermissionService, RolePermissionService>();
builder.Services.AddScoped<ICategoryMeasurementUnitService, CategoryMeasurementUnitService>();
builder.Services.AddScoped<ISharedInventoryService, SharedInventoryService>();
builder.Services.AddScoped<ISaleDetailService, SaleDetailService>();
builder.Services.AddScoped<ICompanyService, CompanyService>();
builder.Services.AddScoped<IEmployeePermissionService, EmployeePermissionService>();

builder.Services.AddScoped<IFcmNotificationService, FcmNotificationService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        }; 
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = InventoryAPI.Security.SessionSecurityValidator.ValidateAsync
        };

    });

try
{
    var firebaseKeyPath = Path.Combine(builder.Environment.ContentRootPath, "firebase-key.json");

    if (File.Exists(firebaseKeyPath))
    {
        string jsonCreds = File.ReadAllText(firebaseKeyPath);

    #pragma warning disable CS0618
        FirebaseApp.Create(new AppOptions()
        {
            Credential = GoogleCredential.FromJson(jsonCreds)
        });
    #pragma warning restore CS0618
    }
    else
    {
        Debug.WriteLine($"[FCM WARNING]: Archivo no encontrado en {firebaseKeyPath}");
    }
}
catch (Exception ex)
{
    Debug.WriteLine($"[FCM ERROR CRÍTICO]: {ex.Message}");
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularApp",
        policy =>
        {
            policy.WithOrigins("http://localhost:4200")
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI();
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/error");
}

app.UseStaticFiles();
app.UseRouting();
//app.UseHttpsRedirection();
app.UseCors("AllowAngularApp");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<AppDbContext>();
        context.Database.Migrate(); // Aplica las tablas

        // 🚀 LA MAGIA DEL SEEDING: Si no hay empresas, creamos la primera
        if (!context.Companies.Any())
        {
            var defaultCompany = new Company
            {
                BusinessName = "Administración Central",
                Ruc = "00000000000",
                LogoUrl = "dotnet_bot.png",
                PrimaryColorHex = "#8A2BE2",
                IsActive = true,
                RegistrationDate = DateTime.Now
            };

            context.Companies.Add(defaultCompany);
            context.SaveChanges();

            int SUPER_ADMIN_ROLE_ID = 1;

            var superAdmins = context.Users
                .Where(u => u.CompanyId == null && u.RoleId == SUPER_ADMIN_ROLE_ID)
                .ToList();

            foreach (var admin in superAdmins)
            {
                admin.CompanyId = defaultCompany.Id;
            }
            context.SaveChanges();
        }

        // Idempotente: aprovisiona el maestro y el inventario personal
        // para empresas/usuarios anteriores a la Fase 3.5 (incluido YosMG).
        InventoryProvisioner.EnsureExistingAsync(context).GetAwaiter().GetResult();

        // Catálogo de permisos compatible con registros existentes; no se alteran
        // permisos ya asignados a los roles.
        // Algunas bases restauradas desde MAUI/SQL tienen los IDs conservados,
        // pero la secuencia identity de PostgreSQL sigue en un valor anterior.
        // Sin sincronizarla, SaveChanges provoca 23505 PK_Permissions.
        // El bloqueo serializa semillas simultáneas y evita IDs duplicados.
        using (var permissionsTx = context.Database.BeginTransaction())
        {
            context.Database.ExecuteSqlRaw(
                "LOCK TABLE \\"Permissions\\" IN SHARE ROW EXCLUSIVE MODE;");
            context.Database.ExecuteSqlRaw("""
                DO $
                DECLARE identity_sequence regclass;
                BEGIN
                    identity_sequence := pg_get_serial_sequence('"Permissions"', 'Id')::regclass;
                    IF identity_sequence IS NULL THEN
                        RAISE EXCEPTION 'No se encontró la secuencia de Permissions.Id';
                    END IF;
                    -- Avanzar la secuencia sin retrocederla: MAX(Id) o su próximo valor.
                    PERFORM setval(identity_sequence,
                        GREATEST(
                            COALESCE((SELECT MAX("Id") FROM "Permissions"), 0),
                            nextval(identity_sequence)
                        ), true);
                END $;
                """);

            var registeredCodes = context.Permissions
                .Select(x => x.SystemCode)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var (code, label) in InventoryPermissionCodes.Definitions)
            {
                if (registeredCodes.Add(code))
                    context.Permissions.Add(new Permission { SystemCode = code, Name = label });
            }
            context.SaveChanges();
            permissionsTx.Commit();
        }
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Ocurrió un error al aplicar las migraciones o preparar los inventarios.");
        // No publicar una API aparentemente sana con el inventario maestro sin crear.
        throw;
    }
}

// Ejecución puntual para trasladar archivos existentes: nunca al arrancar normal.
// Mantiene los originales para que el propietario pueda revisar el resultado.
if (args.Contains("--migrate-private-media", StringComparer.OrdinalIgnoreCase))
{
    using var migrationScope = app.Services.CreateScope();
    var migrator = migrationScope.ServiceProvider.GetRequiredService<LegacyMediaMigrator>();
    await migrator.RunAsync();
    return;
}

app.Run();