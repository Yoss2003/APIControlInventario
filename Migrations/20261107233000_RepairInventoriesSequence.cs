using InventoryAPI.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventoryAPI.Migrations;

/// <summary>
/// Restores made from data-only backups may contain inventory rows with a sequence
/// counter behind the maximum ID. Fix it before InventoryProvisioner creates rows.
/// Does not modify existing inventories or lower an already-ahead sequence.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20261107233000_RepairInventoriesSequence")]
public partial class RepairInventoriesSequence : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $repair_inventory_sequence$
            DECLARE
                sequence_name text;
                last_assigned bigint;
                sequence_called boolean;
                max_inventory_id bigint;
            BEGIN
                sequence_name := pg_get_serial_sequence('public."Inventories"', 'Id');

                IF sequence_name IS NULL THEN
                    RAISE EXCEPTION 'No se encontró la secuencia de Inventories.Id';
                END IF;

                EXECUTE format('SELECT last_value, is_called FROM %s', sequence_name)
                    INTO last_assigned, sequence_called;

                SELECT COALESCE(MAX("Id"), 0)
                    INTO max_inventory_id FROM public."Inventories";

                IF max_inventory_id > last_assigned OR
                    (max_inventory_id = last_assigned AND NOT sequence_called) THEN
                    PERFORM setval(sequence_name::regclass, max_inventory_id, true);
                END IF;
            END;
            $repair_inventory_sequence$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // We deliberately do not rewind the sequence on rollback.
    }
}
