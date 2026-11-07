using InventoryAPI.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventoryAPI.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261107232000_MasterSuppliersUnique")]
public partial class MasterSuppliersUnique : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_Suppliers_Company_Ruc",
            table: "Suppliers",
            columns: new[] { "CompanyId", "Ruc" },
            unique: true,
            filter: "\"IsActive\" = true");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("IX_Suppliers_Company_Ruc", "Suppliers");
    }
}
