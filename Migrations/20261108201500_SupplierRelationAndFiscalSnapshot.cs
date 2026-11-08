using InventoryAPI.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventoryAPI.Migrations;

// Migración aditiva: conserva íntegros los proveedores y sus relaciones existentes.
[DbContext(typeof(AppDbContext))]
[Migration("20261108201500_SupplierRelationAndFiscalSnapshot")]
public partial class SupplierRelationAndFiscalSnapshot : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "RelationshipDescription",
            table: "Suppliers",
            type: "character varying(700)",
            maxLength: 700,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "FiscalDataJson",
            table: "Suppliers",
            type: "character varying(10000)",
            maxLength: 10000,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "FiscalDataJson", table: "Suppliers");
        migrationBuilder.DropColumn(name: "RelationshipDescription", table: "Suppliers");
    }
}
