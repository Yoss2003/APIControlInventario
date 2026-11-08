using InventoryAPI.Models;
using InventoryAPI.Data;
using InventoryAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace InventoryAPI.Controllers;

[ApiController]
[Authorize]
[Route("api/dev")]
public sealed class DevManagementController(AppDbContext db) : ControllerBase
{
    private bool IsDeveloper() => User.FindFirst("RoleId")?.Value == "1" &&
        int.TryParse(User.FindFirst("UserId")?.Value, out var uid) && uid > 0;

    // Se conservan ExpiresAt y las propiedades existentes de manera temporal
    // para no cambiar el contrato de Angular hasta la fase 2.2.
    // IMPORTANTE: ExpiresAt entrante nunca determina el vencimiento.
    public sealed record CompanyInput(
        [Required, MaxLength(255)] string BusinessName,
        [Required, RegularExpression(@"^\d{11}$")] string Ruc,
        string Plan,
        int MaxUsers,
        DateTime? ExpiresAt,
        bool LicenseEnabled,
        string? PrimaryColorHex,
        string? LogoUrl,
        string? FiscalOwnerName = null,
        string? ContactEmail = null);

    public sealed record RenewLicenseInput([Required] string Plan, int? MaxUsers = null);

    // Los cupos son reglas del servidor, nunca valores editables desde Angular.
    // MaxUsers = 0 representa usuarios ilimitados SOLO cuando Plan es Max.
    private static bool TryResolvePlan(string? value, out string plan, out int months, out int maxUsers)
    {
        plan = (value ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "basico" or "básico" or "basic" => "Básico",
            "pro" => "Pro",
            "plus" => "Plus",
            "max" => "Max",
            _ => string.Empty
        };
        months = plan == "Max" ? 3 : 1;
        maxUsers = plan switch
        {
            "Básico" => 5,
            "Pro" => 10,
            "Plus" => 15,
            "Max" => 0,
            _ => -1
        };
        return plan.Length > 0;
    }

    private static string? ValidateCompanyInput(CompanyInput input, bool requireNewFields)
    {
        if (string.IsNullOrWhiteSpace(input.BusinessName) || input.BusinessName.Trim().Length > 255)
            return "Ingresa una razón social válida (máximo 255 caracteres).";
        if (!Regex.IsMatch(input.Ruc ?? "", @"^\d{11}$"))
            return "El RUC debe tener 11 dígitos.";
        if (!string.IsNullOrWhiteSpace(input.PrimaryColorHex) &&
            !Regex.IsMatch(input.PrimaryColorHex, @"^#[0-9a-fA-F]{6}$"))
            return "El color debe tener el formato #RRGGBB.";

        // En altas se exigen ambos campos. En ediciones se permite omitirlos
        // para preservar la compatibilidad con registros y pantallas anteriores.
        if (requireNewFields || input.FiscalOwnerName is not null)
        {
            if (string.IsNullOrWhiteSpace(input.FiscalOwnerName) || input.FiscalOwnerName.Trim().Length > 255)
                return "Ingresa el nombre del dueño fiscal (máximo 255 caracteres).";
        }
        if (requireNewFields || input.ContactEmail is not null)
        {
            var email = input.ContactEmail?.Trim() ?? "";
            if (email.Length == 0 || email.Length > 254 || !new EmailAddressAttribute().IsValid(email))
                return "Ingresa un correo de contacto válido.";
        }
        return null;
    }

    private Task<bool> HasDeveloperUserAsync(int companyId) =>
        db.Users.AnyAsync(u => u.CompanyId == companyId && u.RoleId == 1);

    [HttpGet("companies")]
    public async Task<IActionResult> List()
    {
        if (!IsDeveloper()) return Forbid();
        var companies = await db.Companies.AsNoTracking().OrderBy(x => x.BusinessName)
            .Select(c => new
            {
                c.Id, c.BusinessName, c.Ruc, c.FiscalOwnerName, c.ContactEmail,
                c.IsActive, c.LogoUrl, c.PrimaryColorHex,
                userCount = db.Users.Count(u => u.CompanyId == c.Id && u.IsActive),
                totalUsers = db.Users.Count(u => u.CompanyId == c.Id),
                inventoryCount = db.Inventories.Count(i => i.CompanyId == c.Id && !i.IsMaster),
                license = db.CompanyLicenses.Where(l => l.CompanyId == c.Id).Select(l => new
                { l.Plan, l.MaxUsers, l.StartsAt, l.ExpiresAt, l.IsEnabled }).FirstOrDefault()
            }).ToListAsync();
        return Ok(companies);
    }

    [HttpPost("companies")]
    public async Task<IActionResult> Create([FromBody] CompanyInput input)
    {
        if (!IsDeveloper()) return Forbid();
        var validation = ValidateCompanyInput(input, requireNewFields: true);
        if (validation is not null) return BadRequest(new { error = validation });
        if (!TryResolvePlan(input.Plan, out var plan, out var months, out var maxUsers))
            return BadRequest(new { error = "Selecciona un plan Básico, Pro, Plus o Max." });
        if (await db.Companies.AnyAsync(c => c.Ruc == input.Ruc))
            return Conflict(new { error = "Ya hay una empresa con este RUC." });

        var now = DateTime.UtcNow;
        await using var tx = await db.Database.BeginTransactionAsync();
        var company = new Company
        {
            BusinessName = input.BusinessName.Trim(),
            Ruc = input.Ruc,
            FiscalOwnerName = input.FiscalOwnerName!.Trim(),
            ContactEmail = input.ContactEmail!.Trim(),
            IsActive = true,
            RegistrationDate = now,
            PrimaryColorHex = input.PrimaryColorHex?.Trim(),
            LogoUrl = input.LogoUrl
        };
        db.Companies.Add(company);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg &&
            pg.SqlState == PostgresErrorCodes.UniqueViolation && pg.ConstraintName == "PK_Companies")
        {
            return Conflict(new { error = "El generador de IDs de empresas parece desfasado. Un administrador debe revisar la secuencia de PostgreSQL antes de reintentar." });
        }

        db.CompanyLicenses.Add(new CompanyLicense
        {
            CompanyId = company.Id,
            Plan = plan,
            MaxUsers = maxUsers,
            StartsAt = now,
            ExpiresAt = now.AddMonths(months),
            IsEnabled = input.LicenseEnabled,
            UpdatedAt = now
        });
        // Alta de empresa y maestro forman parte de la MISMA transacción.
        db.Inventories.Add(InventoryProvisioner.NewMaster(company.Id));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Ok(new { company.Id });
    }

    [HttpPut("companies/{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] CompanyInput input)
    {
        if (!IsDeveloper()) return Forbid();
        var validation = ValidateCompanyInput(input, requireNewFields: false);
        if (validation is not null) return BadRequest(new { error = validation });

        var company = await db.Companies.FindAsync(id);
        if (company is null) return NotFound();
        if (await db.Companies.AnyAsync(c => c.Ruc == input.Ruc && c.Id != id))
            return Conflict(new { error = "RUC utilizado por otra empresa." });

        var central = await HasDeveloperUserAsync(id);
        if (central && company.Ruc != input.Ruc)
            return Conflict(new { error = "No se puede cambiar el RUC de la empresa que administra el sistema." });

        // La edición normal NO renueva licencias ni acepta una fecha calculada por Angular.
        var license = await db.CompanyLicenses.SingleOrDefaultAsync(l => l.CompanyId == id);
        if (!central)
        {
            if (license is null)
            {
                // Compatibilidad: una empresa histórica sin licencia puede recibir
                // su primera licencia sin alterar las demás empresas existentes.
                if (!TryResolvePlan(input.Plan, out var newPlan, out var months, out var maxUsers))
                    return BadRequest(new { error = "Selecciona un plan Básico, Pro, Plus o Max." });
                var now = DateTime.UtcNow;
                license = new CompanyLicense
                {
                    CompanyId = id,
                    Plan = newPlan,
                    MaxUsers = maxUsers,
                    StartsAt = now,
                    ExpiresAt = now.AddMonths(months),
                    IsEnabled = input.LicenseEnabled,
                    UpdatedAt = now
                };
                db.CompanyLicenses.Add(license);
            }
            else
            {
                // Conserva planes históricos en edición, pero no permite cambiarlos
                // por esta ruta: el cambio de plan exige renovación explícita.
                if (!string.Equals(input.Plan?.Trim(), license.Plan, StringComparison.OrdinalIgnoreCase))
                    return Conflict(new { error = "Para cambiar de plan, utiliza la renovación de licencia." });
                // El cupo depende del plan y solo cambia al renovar, no al editar.
                license.IsEnabled = input.LicenseEnabled;
                license.UpdatedAt = DateTime.UtcNow;
            }
            if (license is not null && license.MaxUsers > 0)
            {
                var activeUsers = await db.Users.CountAsync(u => u.CompanyId == id && u.IsActive);
                if (license.MaxUsers < activeUsers)
                    return Conflict(new { error = "El plan no admite los usuarios activos de esta empresa." });
            }
        }

        company.BusinessName = input.BusinessName.Trim();
        company.Ruc = input.Ruc;
        if (input.FiscalOwnerName is not null) company.FiscalOwnerName = input.FiscalOwnerName.Trim();
        if (input.ContactEmail is not null) company.ContactEmail = input.ContactEmail.Trim();
        company.PrimaryColorHex = input.PrimaryColorHex?.Trim();
        company.LogoUrl = input.LogoUrl;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("companies/{id:int}/renew-license")]
    public async Task<IActionResult> RenewLicense(int id, [FromBody] RenewLicenseInput input)
    {
        if (!IsDeveloper()) return Forbid();
        if (!TryResolvePlan(input.Plan, out var plan, out var months, out var maxUsers))
            return BadRequest(new { error = "Selecciona un plan Básico, Pro, Plus o Max." });

        var company = await db.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        if (company is null) return NotFound();
        if (!company.IsActive)
            return Conflict(new { error = "No se puede renovar una empresa inactiva." });
        if (await HasDeveloperUserAsync(id))
            return Conflict(new { error = "La licencia de Administración Central no se modifica desde esta operación." });

        var now = DateTime.UtcNow;
        var current = await db.CompanyLicenses.SingleOrDefaultAsync(l => l.CompanyId == id);
        var activeUsers = await db.Users.CountAsync(u => u.CompanyId == id && u.IsActive);
        // No se acepta un cupo manual desde el frontend: se deriva del plan.
        if (maxUsers > 0 && maxUsers < activeUsers)
            return Conflict(new { error = $"El plan {plan} admite hasta {maxUsers} usuarios; la empresa tiene {activeUsers} activos." });

        // Si se renueva antes del vencimiento, se conserva el tiempo pagado.
        // Si ya venció, la nueva vigencia comienza ahora.
        var baseDate = current is not null && current.ExpiresAt > now ? current.ExpiresAt : now;
        if (current is null)
        {
            current = new CompanyLicense { CompanyId = id, StartsAt = now };
            db.CompanyLicenses.Add(current);
        }
        current.StartsAt ??= now;
        current.Plan = plan;
        current.MaxUsers = maxUsers;
        current.ExpiresAt = baseDate.AddMonths(months);
        current.IsEnabled = true;
        current.UpdatedAt = now;
        await db.SaveChangesAsync();
        return Ok(new { current.Plan, current.MaxUsers, current.StartsAt, current.ExpiresAt, current.IsEnabled });
    }

    // Activa de nuevo una empresa existente. No crea ni renueva su licencia.
    [HttpPost("companies/{id:int}/reactivate")]
    public async Task<IActionResult> Reactivate(int id)
    {
        if (!IsDeveloper()) return Forbid();

        var company = await db.Companies.FindAsync(id);
        if (company is null) return NotFound(new { error = "La empresa no existe." });
        if (await HasDeveloperUserAsync(id))
            return Conflict(new { error = "La empresa administradora está protegida." });
        if (company.IsActive)
            return Conflict(new { error = "La empresa ya está activa." });

        company.IsActive = true;
        company.DeletionDate = null;
        company.DeletionUser = null;
        // No modificar CompanyLicenses: vencimiento, plan, cupo y habilitación permanecen intactos.
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("companies/{id:int}")]
    public async Task<IActionResult> Deactivate(int id)
    {
        if (!IsDeveloper()) return Forbid();
        var company = await db.Companies.FindAsync(id);
        if (company is null) return NotFound();
        if (await HasDeveloperUserAsync(id))
            return Conflict(new { error = "No se puede desactivar la empresa administradora." });
        // Nunca borrar en cascada. Se bloquea la baja si está referenciada.
        if (await db.Users.AnyAsync(u => u.CompanyId == id) ||
            await db.Inventories.AnyAsync(i => i.CompanyId == id && !i.IsMaster) ||
            await db.Categories.AnyAsync(c => c.CompanyId == id) ||
            await db.Articles.AnyAsync(a => a.CompanyId == id) ||
            await db.Movements.AnyAsync(m => m.CompanyId == id))
            return Conflict(new { error = "La empresa tiene usuarios, inventarios u operaciones. Suspende su licencia si deseas bloquear el acceso." });
        company.IsActive = false;
        company.DeletionDate = DateTime.UtcNow;
        company.DeletionUser = User.FindFirst("UserId")?.Value;
        await db.SaveChangesAsync();
        return NoContent();
    }
}
