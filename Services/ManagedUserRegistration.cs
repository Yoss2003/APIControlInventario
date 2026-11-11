using InventoryAPI.Data;
using InventoryAPI.Models;
using InventoryAPI.Models.DTO;
using InventoryAPI.Security;
using InventoryAPI.Services.IServices;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace InventoryAPI.Services;

/// <summary>Alta atómica de usuarios e inventarios personales, sin SQL manual ni envío de claves por correo.</summary>
public sealed class ManagedUserRegistration(AppDbContext db, ICompanyLicenseService licenses)
{
    public async Task<(bool Success, string? Error, object? Created)> RegisterAsync(
        int companyId, ManagedUserInput input, bool firstAdministrator)
    {
        if (companyId <= 0) return (false, "Empresa inválida.", null);
        if (firstAdministrator && input.RoleId != 2) return (false, "El primer administrador requiere rol 2.", null);
        if (!firstAdministrator && input.RoleId is 1 or 2) return (false, "Los roles privilegiados se registran por un proceso separado.", null);
        if (input.RoleId <= 0 || !await db.Roles.AnyAsync(r => r.Id == input.RoleId))
            return (false, "Rol inválido o no configurado.", null);
        var username = input.Username?.Trim() ?? "";
        var email = input.Email?.Trim() ?? "";
        var firstName = input.FirstName?.Trim() ?? "";
        var lastName = input.LastName?.Trim() ?? "";
        if (username.Length < 3 || username.Length > 80 || firstName.Length == 0 ||
            lastName.Length == 0 || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email) ||
            string.IsNullOrWhiteSpace(input.InitialPassword) || input.InitialPassword.Length < 12)
            return (false, "Completa el nombre, usuario, correo y clave (mínimo 12 caracteres).", null);

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            if (!await db.Companies.AnyAsync(c => c.Id == companyId && c.IsActive))
                return (false, "La empresa no existe o está inactiva.", null);
            if (firstAdministrator && await db.Users.AnyAsync(u => u.CompanyId == companyId && u.RoleId == 2))
                return (false, "La empresa ya tiene un administrador. No se creará uno duplicado.", null);
            var allowed = await licenses.CanCreateUserAsync(companyId);
            if (!allowed.Allowed) return (false, allowed.Error, null);

            if (await db.Users.AnyAsync(u => u.Username == username))
                return (false, "El nombre de usuario ya está registrado.", null);
            if (await db.Users.AnyAsync(u => u.Email == email))
                return (false, "El correo ya está registrado.", null);
            if (!string.IsNullOrWhiteSpace(input.Dni) &&
                await db.Employees.AnyAsync(e => e.DNI == input.Dni && e.CompanyId == companyId))
                return (false, "El DNI ya está registrado en la empresa.", null);

            var user = new User
            {
                CompanyId = companyId,
                RoleId = input.RoleId,
                Username = username,
                Email = email,
                Password = PasswordHashService.HashPassword(input.InitialPassword),
                IsActive = true, StatusId = 1, MustChangePassword = false,
                CreatedAt = DateTime.UtcNow
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();

            db.Employees.Add(new Employee
            {
                UserId = user.Id, CompanyId = companyId,
                FirstName = firstName, LastName = lastName,
                DNI = input.Dni?.Trim(), IsActive = true, StatusId = 1
            });
            var personal = InventoryProvisioner.NewPersonal(user);
            db.Inventories.Add(personal);
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return (true, null, new
            {
                user.Id, user.Username, user.Email, user.RoleId, user.CompanyId,
                firstName, lastName, personalInventoryId = personal.Id,
                isActive = true
            });
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync();
            return (false, "No se pudo crear el usuario. Verifica referencias, duplicados y el estado de PostgreSQL.", null);
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            return (false, "No se pudo completar el alta del usuario. Reintenta la operación.", null);
        }
    }
}
