using System.ComponentModel.DataAnnotations;
using System.Data;
using InventoryAPI.Data;
using InventoryAPI.Models;
using InventoryAPI.Models.DTO;
using InventoryAPI.Security;
using InventoryAPI.Services.IServices;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace InventoryAPI.Services;

/// <summary>
/// Alta del primer administrador y del personal. Un solo inventorío operativo por usuario.
/// La contraseña se genera en servidor y solo se entrega por correo al destinatario.
/// </summary>
public sealed class ManagedUserRegistration(
    AppDbContext db, ICompanyLicenseService licenses,
    UserCredentialMailer mailer, ILogger<ManagedUserRegistration> logger)
{
    public async Task<(bool Success, string? Error, object? Created)> RegisterAsync(
        int companyId, ManagedUserInput input, bool firstAdministrator)
    {
        if (companyId <= 0) return (false, "Empresa inválida.", null);
        if (firstAdministrator && input.RoleId != 2)
            return (false, "El primer administrador debe tener rol 2.", null);
        if (!firstAdministrator && input.RoleId <= 2)
            return (false, "El rol de desarrollador o administrador no se asigna al personal.", null);
        if (!await db.Roles.AnyAsync(r => r.Id == input.RoleId))
            return (false, "Selecciona un rol registrado.", null);
        var email = input.Email?.Trim() ?? "";
        var firstName = input.FirstName?.Trim() ?? "";
        var lastName = input.LastName?.Trim() ?? "";
        var dni = input.Dni?.Trim();
        var name = ManagedUsername.FromNames(firstName, lastName);
        if (name.Length < 3 || firstName.Length > 100 || lastName.Length > 100 ||
            !new EmailAddressAttribute().IsValid(email) || email.Length > 254 ||
            (!string.IsNullOrEmpty(dni) && (dni.Length != 8 || !dni.All(char.IsDigit))))
            return (false, "Nombre, apellidos, correo o DNI inválidos.", null);
        if (!await mailer.IsConfiguredAsync(companyId))
            return (false, "No hay correo SMTP configurado. Configúralo en Ajustes antes de crear cuentas.", null);

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            if (!await db.Companies.AnyAsync(c => c.Id == companyId && c.IsActive))
                return (false, "La empresa no existe o está inactiva.", null);
            if (firstAdministrator && await db.Users.AnyAsync(u => u.CompanyId == companyId && u.RoleId == 2))
                return (false, "La empresa ya tiene un administrador. No se creará otro inicial.", null);
            var allowed = await licenses.CanCreateUserAsync(companyId);
            if (!allowed.Allowed) return (false, allowed.Error, null);
            if (await db.Users.AnyAsync(u => u.Email != null && u.Email.ToLower() == email.ToLower()))
                return (false, "El correo ya está asociado a otra cuenta.", null);
            if (!string.IsNullOrEmpty(dni) && await db.Employees.AnyAsync(e =>
                    e.CompanyId == companyId && e.DNI == dni))
                return (false, "El DNI ya está registrado en esta empresa.", null);

            // Evita pedir al administrador que invente variantes manuales si hay nombres repetidos.
            var candidate = name;
            for (var suffix = 2; suffix < 10000; suffix++)
            {
                if (!await db.Users.AnyAsync(u => u.Username != null &&
                    u.Username.ToLower() == candidate.ToLower())) break;
                candidate = name + suffix;
            }
            if (await db.Users.AnyAsync(u => u.Username == candidate))
                return (false, "No fue posible generar un nombre de usuario único.", null);

            var temporaryPassword = UserCredentialMailer.NewTemporaryPassword();
            var user = new User
            {
                CompanyId = companyId,
                RoleId = input.RoleId,
                Username = candidate,
                Email = email,
                Password = PasswordHashService.HashPassword(temporaryPassword),
                IsActive = true,
                StatusId = 1,
                MustChangePassword = true,
                CreatedAt = DateTime.UtcNow
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();

            db.Employees.Add(new Employee
            {
                UserId = user.Id, CompanyId = companyId,
                FirstName = firstName, LastName = lastName,
                DNI = dni, IsActive = true, StatusId = 1
            });
            var personalInventory = InventoryProvisioner.NewPersonal(user);
            db.Inventories.Add(personalInventory);
            await db.SaveChangesAsync();
            await tx.CommitAsync();

            // Correo después del commit: un fallo SMTP no revierte un alta válida.
            // Se permite corregir el email y reenviar una credencial nueva desde Gestión.
            var delivery = await mailer.SendAsync(companyId, email, firstName, candidate, temporaryPassword);
            return (true, null, new
            {
                user.Id, user.Username, user.Email, user.RoleId, user.CompanyId,
                firstName, lastName, personalInventoryId = personalInventory.Id,
                emailSent = delivery.Sent,
                message = delivery.Sent
                    ? "Usuario e inventario creados. Credenciales enviadas al correo."
                    : delivery.Message
            });
        }
        catch (DbUpdateException ex)
        {
            await tx.RollbackAsync();
            if (ex.InnerException is PostgresException pg)
            {
                logger.LogError(ex,
                    "Alta usuario PostgreSQL {SqlState} tabla {Table} restricción {Constraint} empresa {CompanyId}",
                    pg.SqlState, pg.TableName, pg.ConstraintName, companyId);
                if (pg.SqlState == PostgresErrorCodes.UniqueViolation)
                    return (false, "Registro duplicado en " + (pg.TableName ?? "base de datos") +
                        ". Revisa el correo y vuelve a intentar. Si persiste, consulta el registro de la API.", null);
                if (pg.SqlState == PostgresErrorCodes.ForeignKeyViolation)
                    return (false, "Falta una referencia de configuración (" +
                        (pg.ConstraintName ?? "FK") + "). Revisa los datos base de PostgreSQL.", null);
            }
            logger.LogError(ex, "No se pudo crear usuario e inventario en empresa {CompanyId}", companyId);
            return (false, "Error al guardar usuario/inventario. Consulta el registro de la API.", null);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            logger.LogError(ex, "Fallo del alta de usuario en empresa {CompanyId}", companyId);
            return (false, "No fue posible completar el registro. Consulta los registros de la API.", null);
        }
    }
}
