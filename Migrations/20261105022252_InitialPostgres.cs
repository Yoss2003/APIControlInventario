using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace InventoryAPI.Migrations
{
    /// <inheritdoc />
    public partial class InitialPostgres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountsReceivables",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MovementId = table.Column<int>(type: "integer", nullable: false),
                    InstallmentNumber = table.Column<int>(type: "integer", nullable: false),
                    InstallmentAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PaidAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    LateFeeAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    DueDate = table.Column<string>(type: "text", nullable: false),
                    PaymentDate = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Frequency = table.Column<string>(type: "text", nullable: false),
                    CompanyId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountsReceivables", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Actions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Actions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Articles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InventoryId = table.Column<int>(type: "integer", nullable: false),
                    CategoryId = table.Column<int>(type: "integer", nullable: false),
                    BrandId = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Barcode = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    Model = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    SerialNumber = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Tracking = table.Column<int>(type: "integer", nullable: false),
                    Presentation = table.Column<string>(type: "text", nullable: true),
                    LocationId = table.Column<int>(type: "integer", nullable: true),
                    StatusId = table.Column<int>(type: "integer", nullable: true),
                    ConditionId = table.Column<int>(type: "integer", nullable: true),
                    Stock = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    MeasurementUnit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AcquisitionUnit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SaleUnit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ConversionFactor = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    AcquisitionPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    AcquisitionCurrency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    SalePrice = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    SaleCurrency = table.Column<string>(type: "text", nullable: true),
                    SupplierId = table.Column<int>(type: "integer", nullable: true),
                    CurrentEmployeeId = table.Column<int>(type: "integer", nullable: true),
                    PreviousEmployeeId = table.Column<int>(type: "integer", nullable: true),
                    FixedAsset = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UsefulLifeMonths = table.Column<int>(type: "integer", nullable: true),
                    WarrantyEndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RegistrationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AcquisitionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModificationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DepartureDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecommissionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActionId = table.Column<int>(type: "integer", nullable: false),
                    Characteristics = table.Column<string>(type: "text", nullable: true),
                    Observation = table.Column<string>(type: "text", nullable: true),
                    MainPhotoPath = table.Column<string>(type: "text", nullable: true),
                    MainVoucherPath = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LastModified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompanyId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Articles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Brands",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InventoryId = table.Column<int>(type: "integer", nullable: false),
                    CategoryId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CompanyId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Brands", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InventoryId = table.Column<int>(type: "integer", nullable: false),
                    ParentCategoryId = table.Column<int>(type: "integer", nullable: true),
                    TrackingMode = table.Column<string>(type: "text", nullable: true),
                    NamingMethod = table.Column<string>(type: "text", nullable: true),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    IsReturnable = table.Column<bool>(type: "boolean", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreationUser = table.Column<string>(type: "text", nullable: true),
                    ModificationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModificationUser = table.Column<string>(type: "text", nullable: true),
                    DeletionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletionUser = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    Label1 = table.Column<string>(type: "text", nullable: true),
                    IsUnique1 = table.Column<bool>(type: "boolean", nullable: false),
                    Label2 = table.Column<string>(type: "text", nullable: true),
                    IsUnique2 = table.Column<bool>(type: "boolean", nullable: false),
                    Label3 = table.Column<string>(type: "text", nullable: true),
                    IsUnique3 = table.Column<bool>(type: "boolean", nullable: false),
                    Label4 = table.Column<string>(type: "text", nullable: true),
                    IsUnique4 = table.Column<bool>(type: "boolean", nullable: false),
                    Label5 = table.Column<string>(type: "text", nullable: true),
                    IsUnique5 = table.Column<bool>(type: "boolean", nullable: false),
                    Label6 = table.Column<string>(type: "text", nullable: true),
                    IsUnique6 = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Companies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BusinessName = table.Column<string>(type: "text", nullable: false),
                    Ruc = table.Column<string>(type: "text", nullable: false),
                    LogoUrl = table.Column<string>(type: "text", nullable: true),
                    PrimaryColorHex = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    RegistrationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletionUser = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Companies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Currencies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CurrencyCode = table.Column<string>(type: "text", nullable: true),
                    CurrencyName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Currencies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Address = table.Column<string>(type: "text", nullable: true),
                    RegistrationDate = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CompanyId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DateFormats",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FormatName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DateFormats", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmployeePermissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    CanApplyDiscounts = table.Column<bool>(type: "boolean", nullable: false),
                    CanViewPurchaseCosts = table.Column<bool>(type: "boolean", nullable: false),
                    CanCancelSales = table.Column<bool>(type: "boolean", nullable: false),
                    CanEditProducts = table.Column<bool>(type: "boolean", nullable: false),
                    CanViewProfits = table.Column<bool>(type: "boolean", nullable: false),
                    CompanyId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeePermissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExchangeRates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BaseCurrency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    QuoteCurrency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    BuyPrice = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    SellPrice = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExchangeRates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExportRoutes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    DefaultRoute1 = table.Column<string>(type: "text", nullable: true),
                    CustomRoute1 = table.Column<string>(type: "text", nullable: true),
                    DefaultRoute2 = table.Column<string>(type: "text", nullable: true),
                    CustomRoute2 = table.Column<string>(type: "text", nullable: true),
                    FileType1 = table.Column<string>(type: "text", nullable: true),
                    FileType2 = table.Column<string>(type: "text", nullable: true),
                    IsDefault1 = table.Column<bool>(type: "boolean", nullable: false),
                    IsDefault2 = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExportRoutes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HistoryLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LogDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Username = table.Column<string>(type: "text", nullable: false),
                    ModuleName = table.Column<string>(type: "text", nullable: false),
                    ActionName = table.Column<string>(type: "text", nullable: false),
                    Detail = table.Column<string>(type: "text", nullable: true),
                    CompanyId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoryLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InstallmentPayments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InstallmentId = table.Column<int>(type: "integer", nullable: false),
                    PaymentAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    PaymentDate = table.Column<string>(type: "text", nullable: false),
                    Observation = table.Column<string>(type: "text", nullable: true),
                    CompanyId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstallmentPayments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Inventories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InventoryName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModificationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Username = table.Column<string>(type: "text", nullable: false),
                    Alias = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CompanyId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inventories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Languages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LanguageName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Languages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MeasurementUnits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UnitName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Abbreviation = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeasurementUnits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Movements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ArticleId = table.Column<int>(type: "integer", nullable: false),
                    EmployeeId = table.Column<int>(type: "integer", nullable: true),
                    ActionId = table.Column<int>(type: "integer", nullable: false),
                    MovementDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Observation = table.Column<string>(type: "text", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric", nullable: true),
                    Recipient = table.Column<string>(type: "text", nullable: true),
                    SalePrice = table.Column<decimal>(type: "numeric", nullable: true),
                    DocumentNumber = table.Column<string>(type: "text", nullable: true),
                    PaymentMethod = table.Column<string>(type: "text", nullable: true),
                    VoucherType = table.Column<string>(type: "text", nullable: true),
                    CustomerPhone = table.Column<string>(type: "text", nullable: true),
                    CustomerEmail = table.Column<string>(type: "text", nullable: true),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    PhotoPath = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Movements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NotificationName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    CompanyId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Parameters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InventoryId = table.Column<int>(type: "integer", nullable: false),
                    ParameterType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    CompanyId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parameters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Permissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SystemCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Profiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Username = table.Column<string>(type: "text", nullable: false),
                    LanguageId = table.Column<int>(type: "integer", nullable: true),
                    ThemeId = table.Column<int>(type: "integer", nullable: true),
                    NotificationId = table.Column<int>(type: "integer", nullable: true),
                    DateFormatId = table.Column<int>(type: "integer", nullable: true),
                    CurrencyId = table.Column<int>(type: "integer", nullable: true),
                    MeasurementUnitId = table.Column<int>(type: "integer", nullable: true),
                    TimeZoneId = table.Column<int>(type: "integer", nullable: true),
                    SalesModeId = table.Column<int>(type: "integer", nullable: true),
                    UseAuthentication = table.Column<bool>(type: "boolean", nullable: false),
                    SharedActivity = table.Column<bool>(type: "boolean", nullable: false),
                    UseBarcodes = table.Column<bool>(type: "boolean", nullable: false),
                    CalculateDevaluation = table.Column<bool>(type: "boolean", nullable: false),
                    GenerateCodes = table.Column<bool>(type: "boolean", nullable: false),
                    ApplyLateFee = table.Column<bool>(type: "boolean", nullable: false),
                    LateFeePercentage = table.Column<float>(type: "real", nullable: true),
                    GraceDays = table.Column<int>(type: "integer", nullable: true),
                    SmtpEmail = table.Column<string>(type: "text", nullable: true),
                    SmtpPassword = table.Column<string>(type: "text", nullable: true),
                    SmtpApproverEmail = table.Column<string>(type: "text", nullable: true),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    QrBilletera = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Profiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RecoveryAttempts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Username = table.Column<string>(type: "text", nullable: false),
                    FailedAttempts = table.Column<int>(type: "integer", nullable: false),
                    LastAttemptDate = table.Column<string>(type: "text", nullable: true),
                    BlockedUntil = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecoveryAttempts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SalesModes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SalesModeName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesModes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SecurityQuestions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Username = table.Column<string>(type: "text", nullable: false),
                    Question1Id = table.Column<int>(type: "integer", nullable: false),
                    Question1 = table.Column<string>(type: "text", nullable: false),
                    Answer1 = table.Column<string>(type: "text", nullable: false),
                    Question2Id = table.Column<int>(type: "integer", nullable: false),
                    Question2 = table.Column<string>(type: "text", nullable: false),
                    Answer2 = table.Column<string>(type: "text", nullable: false),
                    Question3Id = table.Column<int>(type: "integer", nullable: false),
                    Question3 = table.Column<string>(type: "text", nullable: false),
                    Answer3 = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SecurityQuestions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Suppliers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InventoryId = table.Column<int>(type: "integer", nullable: false),
                    Ruc = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false),
                    BusinessName = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    ContactName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    StatusId = table.Column<int>(type: "integer", nullable: false),
                    Estado = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Condicion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Distrito = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Provincia = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Departamento = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CompanyId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Suppliers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Themes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ThemeName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Themes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TimeZones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TimeZoneName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimeZones", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ArticleDetails",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ArticleId = table.Column<int>(type: "integer", nullable: false),
                    SerialNumber = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Attr1 = table.Column<string>(type: "text", nullable: true),
                    Attr2 = table.Column<string>(type: "text", nullable: true),
                    Attr3 = table.Column<string>(type: "text", nullable: true),
                    Attr4 = table.Column<string>(type: "text", nullable: true),
                    Attr5 = table.Column<string>(type: "text", nullable: true),
                    Attr6 = table.Column<string>(type: "text", nullable: true),
                    StatusId = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    RegistrationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArticleDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArticleDetails_Articles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "Articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CategoryMeasurementUnits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CategoryId = table.Column<int>(type: "integer", nullable: false),
                    MeasurementUnitId = table.Column<int>(type: "integer", nullable: false),
                    CompanyId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoryMeasurementUnits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CategoryMeasurementUnits_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<int>(type: "integer", nullable: false),
                    PermissionId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RolePermissions_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RolePermissions_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Username = table.Column<string>(type: "text", nullable: true),
                    Password = table.Column<string>(type: "text", nullable: true),
                    Email = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    ProfilePictureUrl = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    RoleId = table.Column<int>(type: "integer", nullable: false),
                    CompanyId = table.Column<int>(type: "integer", nullable: true),
                    StatusId = table.Column<int>(type: "integer", nullable: false),
                    MustChangePassword = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorSecret = table.Column<string>(type: "text", nullable: true),
                    IsTwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FcmToken = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Users_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Employees",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    FirstName = table.Column<string>(type: "text", nullable: true),
                    LastName = table.Column<string>(type: "text", nullable: true),
                    DNI = table.Column<string>(type: "text", nullable: true),
                    Age = table.Column<int>(type: "integer", nullable: true),
                    BirthDate = table.Column<string>(type: "text", nullable: true),
                    JobPositionId = table.Column<int>(type: "integer", nullable: true),
                    AreaId = table.Column<int>(type: "integer", nullable: true),
                    HireDate = table.Column<string>(type: "text", nullable: true),
                    ContractTypeId = table.Column<int>(type: "integer", nullable: true),
                    StatusId = table.Column<int>(type: "integer", nullable: false),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Employees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Employees_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Sales",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    SaleDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PaymentType = table.Column<int>(type: "integer", nullable: false),
                    SalesModeId = table.Column<int>(type: "integer", nullable: false),
                    CustomerName = table.Column<string>(type: "text", nullable: true),
                    TotalAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    AmountReceived = table.Column<decimal>(type: "numeric", nullable: true),
                    ChangeGiven = table.Column<decimal>(type: "numeric", nullable: true),
                    CompanyId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sales", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sales_SalesModes_SalesModeId",
                        column: x => x.SalesModeId,
                        principalTable: "SalesModes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Sales_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SharedInventories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InventoryId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    AccessLevel = table.Column<int>(type: "integer", nullable: false),
                    SharedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompanyId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SharedInventories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SharedInventories_Inventories_InventoryId",
                        column: x => x.InventoryId,
                        principalTable: "Inventories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SharedInventories_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SaleDetails",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SaleId = table.Column<int>(type: "integer", nullable: false),
                    ArticleId = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    SubTotal = table.Column<decimal>(type: "numeric", nullable: false),
                    CompanyId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaleDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SaleDetails_Sales_SaleId",
                        column: x => x.SaleId,
                        principalTable: "Sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArticleDetails_ArticleId",
                table: "ArticleDetails",
                column: "ArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_CategoryMeasurementUnits_CategoryId",
                table: "CategoryMeasurementUnits",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_UserId",
                table: "Employees",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_PermissionId",
                table: "RolePermissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_RoleId",
                table: "RolePermissions",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleDetails_SaleId",
                table: "SaleDetails",
                column: "SaleId");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_SalesModeId",
                table: "Sales",
                column: "SalesModeId");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_UserId",
                table: "Sales",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SharedInventories_InventoryId",
                table: "SharedInventories",
                column: "InventoryId");

            migrationBuilder.CreateIndex(
                name: "IX_SharedInventories_UserId",
                table: "SharedInventories",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_CompanyId",
                table: "Users",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_RoleId",
                table: "Users",
                column: "RoleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountsReceivables");

            migrationBuilder.DropTable(
                name: "Actions");

            migrationBuilder.DropTable(
                name: "ArticleDetails");

            migrationBuilder.DropTable(
                name: "Brands");

            migrationBuilder.DropTable(
                name: "CategoryMeasurementUnits");

            migrationBuilder.DropTable(
                name: "Currencies");

            migrationBuilder.DropTable(
                name: "Customers");

            migrationBuilder.DropTable(
                name: "DateFormats");

            migrationBuilder.DropTable(
                name: "EmployeePermissions");

            migrationBuilder.DropTable(
                name: "Employees");

            migrationBuilder.DropTable(
                name: "ExchangeRates");

            migrationBuilder.DropTable(
                name: "ExportRoutes");

            migrationBuilder.DropTable(
                name: "HistoryLogs");

            migrationBuilder.DropTable(
                name: "InstallmentPayments");

            migrationBuilder.DropTable(
                name: "Languages");

            migrationBuilder.DropTable(
                name: "MeasurementUnits");

            migrationBuilder.DropTable(
                name: "Movements");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "Parameters");

            migrationBuilder.DropTable(
                name: "Profiles");

            migrationBuilder.DropTable(
                name: "RecoveryAttempts");

            migrationBuilder.DropTable(
                name: "RolePermissions");

            migrationBuilder.DropTable(
                name: "SaleDetails");

            migrationBuilder.DropTable(
                name: "SecurityQuestions");

            migrationBuilder.DropTable(
                name: "SharedInventories");

            migrationBuilder.DropTable(
                name: "Suppliers");

            migrationBuilder.DropTable(
                name: "Themes");

            migrationBuilder.DropTable(
                name: "TimeZones");

            migrationBuilder.DropTable(
                name: "Articles");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropTable(
                name: "Permissions");

            migrationBuilder.DropTable(
                name: "Sales");

            migrationBuilder.DropTable(
                name: "Inventories");

            migrationBuilder.DropTable(
                name: "SalesModes");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Companies");

            migrationBuilder.DropTable(
                name: "Roles");
        }
    }
}
