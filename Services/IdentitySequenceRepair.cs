using InventoryAPI.Data;
using Microsoft.EntityFrameworkCore;

namespace InventoryAPI.Services;

/// <summary>Sincroniza secuencias de tablas restauradas desde MAUI/SQL Server.</summary>
public static class IdentitySequenceRepair
{
    public static void Synchronize(AppDbContext db)
    {
        using var transaction = db.Database.BeginTransaction();
        db.Database.ExecuteSqlRaw(
            """LOCK TABLE "Users", "Employees", "Inventories", "RolePermissions", "PasswordRecoveryRequests" IN SHARE ROW EXCLUSIVE MODE;""");

        // La lista es constante, nunca proviene de datos del usuario.
        foreach (var table in new[] { "Users", "Employees", "Inventories", "RolePermissions", "PasswordRecoveryRequests" })
        {
            db.Database.ExecuteSqlRaw($"""
                SELECT setval(
                  pg_get_serial_sequence('"{table}"', 'Id'),
                  GREATEST(
                    COALESCE((SELECT MAX("Id") FROM "{table}"), 0),
                    nextval(pg_get_serial_sequence('"{table}"', 'Id'))
                  ),
                  true
                );
                """);
        }
        transaction.Commit();
    }
}
