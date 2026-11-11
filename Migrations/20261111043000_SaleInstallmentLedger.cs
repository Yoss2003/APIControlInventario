using InventoryAPI.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace InventoryAPI.Migrations;

// Migración exclusivamente aditiva: no modifica cuentas por cobrar ni cobros heredados.
[DbContext(typeof(AppDbContext))]
[Migration("20261111043000_SaleInstallmentLedger")]
public partial class SaleInstallmentLedger : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "SaleInstallments",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                SaleId = table.Column<int>(type: "integer", nullable: false),
                CompanyId = table.Column<int>(type: "integer", nullable: false),
                InstallmentNumber = table.Column<int>(type: "integer", nullable: false),
                InstallmentAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                PaidAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                Frequency = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SaleInstallments", x => x.Id);
                table.ForeignKey("FK_SaleInstallments_Sales_SaleId", x => x.SaleId,
                    "Sales", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "SaleInstallmentReceipts",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                SaleInstallmentId = table.Column<int>(type: "integer", nullable: false),
                CompanyId = table.Column<int>(type: "integer", nullable: false),
                Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                PaymentType = table.Column<int>(type: "integer", nullable: false),
                ReceivedByUserId = table.Column<int>(type: "integer", nullable: false),
                Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                OperationKey = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SaleInstallmentReceipts", x => x.Id);
                table.ForeignKey("FK_SaleInstallmentReceipts_SaleInstallments_SaleInstallmentId",
                    x => x.SaleInstallmentId, "SaleInstallments", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_SaleInstallments_SaleId_InstallmentNumber", table: "SaleInstallments",
            columns: new[] { "SaleId", "InstallmentNumber" }, unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_SaleInstallments_CompanyId_DueDate", table: "SaleInstallments",
            columns: new[] { "CompanyId", "DueDate" });
        migrationBuilder.CreateIndex(
            name: "IX_SaleInstallmentReceipts_OperationKey", table: "SaleInstallmentReceipts",
            column: "OperationKey", unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_SaleInstallmentReceipts_CompanyId_SaleInstallmentId",
            table: "SaleInstallmentReceipts", columns: new[] { "CompanyId", "SaleInstallmentId" });
        migrationBuilder.CreateIndex(
            name: "IX_SaleInstallmentReceipts_SaleInstallmentId",
            table: "SaleInstallmentReceipts", column: "SaleInstallmentId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "SaleInstallmentReceipts");
        migrationBuilder.DropTable(name: "SaleInstallments");
    }
}
