using InventoryAPI.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventoryAPI.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261107231000_MasterCatalogKeys")]
public partial class MasterCatalogKeys : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "NameKey", table: "Categories", type: "text", nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>(
            name: "NameKey", table: "Brands", type: "text", nullable: false, defaultValue: "");

        // Mismo criterio que CatalogNames.Key para nombres comunes en español:
        // minúsculas, espacios colapsados y tildes eliminadas, sin alterar "Name".
        // No requiere instalar extensiones en PostgreSQL.
        migrationBuilder.Sql("""
            UPDATE "Categories" SET "NameKey" =
                translate(lower(btrim(regexp_replace("Name", '[[:space:]]+', ' ', 'g'))),
                  'áàâäãåéèêëíìîïóòôöõúùûüñçýÿ',
                  'aaaaaaeeeeiiiiooooouuuuncyy');
            UPDATE "Brands" SET "NameKey" =
                translate(lower(btrim(regexp_replace("Name", '[[:space:]]+', ' ', 'g'))),
                  'áàâäãåéèêëíìîïóòôöõúùûüñçýÿ',
                  'aaaaaaeeeeiiiiooooouuuuncyy');
            """);

        // Evita dos registros activos equivalentes en la misma empresa/categoría.
        // Para categorías raíz se requiere un índice separado por el ParentCategoryId NULL.
        migrationBuilder.CreateIndex(
            name: "IX_Categories_MasterRoot_NameKey",
            table: "Categories",
            columns: new[] { "CompanyId", "NameKey" },
            unique: true,
            filter: "\"ParentCategoryId\" IS NULL AND \"IsActive\" = true");

        migrationBuilder.CreateIndex(
            name: "IX_Categories_MasterChild_NameKey",
            table: "Categories",
            columns: new[] { "CompanyId", "ParentCategoryId", "NameKey" },
            unique: true,
            filter: "\"ParentCategoryId\" IS NOT NULL AND \"IsActive\" = true");

        migrationBuilder.CreateIndex(
            name: "IX_Brands_Company_Category_NameKey",
            table: "Brands",
            columns: new[] { "CompanyId", "CategoryId", "NameKey" },
            unique: true,
            filter: "\"IsActive\" = true");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("IX_Brands_Company_Category_NameKey", "Brands");
        migrationBuilder.DropIndex("IX_Categories_MasterChild_NameKey", "Categories");
        migrationBuilder.DropIndex("IX_Categories_MasterRoot_NameKey", "Categories");
        migrationBuilder.DropColumn("NameKey", "Brands");
        migrationBuilder.DropColumn("NameKey", "Categories");
    }
}
