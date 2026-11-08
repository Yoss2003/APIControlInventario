using InventoryAPI.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventoryAPI.Migrations;

/// <summary>
/// Diferencia los inventarios maestros y evita más de uno por empresa.
/// Las filas existentes permanecen como inventarios personales.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20261107230000_InventoryMasterFoundation")]
public partial class InventoryMasterFoundation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsMaster",
            table: "Inventories",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.CreateIndex(
            name: "IX_Inventories_CompanyId_Master",
            table: "Inventories",
            column: "CompanyId",
            unique: true,
            filter: "\"IsMaster\" = true");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Inventories_CompanyId_Master",
            table: "Inventories");

        migrationBuilder.DropColumn(
            name: "IsMaster",
            table: "Inventories");
    }
}
