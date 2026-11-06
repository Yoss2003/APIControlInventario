using ControlInventario.Shared.Models;
using InventoryAPI.Data;
using InventoryAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace InventoryAPI.Controllers;

[ApiController]
[Authorize]
[Route("api/dev")]
public sealed class DevManagementController(AppDbContext db) : ControllerBase
{
    private bool IsDeveloper() => User.FindFirst("RoleId")?.Value == "1" &&
        int.TryParse(User.FindFirst("UserId")?.Value, out var uid) && uid > 0;

    public sealed record CompanyInput(
        [Required, MaxLength(255)] string BusinessName,
        [Required, RegularExpression(@"^\d{11}$")] string Ruc,
        string Plan, int MaxUsers, DateTime ExpiresAt, bool LicenseEnabled,
        string? PrimaryColorHex, string? LogoUrl);

    [HttpGet("companies")]
    public async Task<IActionResult> List()
    {
        if (!IsDeveloper()) return Forbid();
        var companies = await db.Companies.AsNoTracking().OrderBy(x => x.BusinessName)
            .Select(c => new
            {
                c.Id, c.BusinessName, c.Ruc, c.IsActive, c.LogoUrl, c.PrimaryColorHex,
                userCount = db.Users.Count(u => u.CompanyId == c.Id && u.IsActive),
                totalUsers = db.Users.Count(u => u.CompanyId == c.Id),
                inventoryCount = db.Inventories.Count(i => i.CompanyId == c.Id),
                license = db.CompanyLicenses.Where(l => l.CompanyId == c.Id).Select(l => new
                { l.Plan, l.MaxUsers, l.ExpiresAt, l.IsEnabled }).FirstOrDefault()
            }).ToListAsync();
        return Ok(companies);
    }

    [HttpPost("companies")]
    public async Task<IActionResult> Create([FromBody] CompanyInput input)
    {
        if (!IsDeveloper()) return Forbid();
        if (!ModelState.IsValid || input.MaxUsers <= 0 || input.ExpiresAt <= DateTime.UtcNow ||
            string.IsNullOrWhiteSpace(input.BusinessName)) return BadRequest(new { error = "Datos de empresa o licencia inválidos." });
        if (await db.Companies.AnyAsync(c => c.Ruc == input.Ruc))
            return Conflict(new { error = "Ya hay una empresa con este RUC." });
        await using var tx = await db.Database.BeginTransactionAsync();
        var company = new Company { BusinessName = input.BusinessName.Trim(), Ruc = input.Ruc,
            IsActive = true, RegistrationDate = DateTime.UtcNow,
            PrimaryColorHex = input.PrimaryColorHex, LogoUrl = input.LogoUrl };
        db.Companies.Add(company); await db.SaveChangesAsync();
        db.CompanyLicenses.Add(new CompanyLicense { CompanyId = company.Id, Plan = input.Plan.Trim(),
            MaxUsers = input.MaxUsers, ExpiresAt = input.ExpiresAt.ToUniversalTime(),
            IsEnabled = input.LicenseEnabled, UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(); await tx.CommitAsync();
        return Ok(new { company.Id });
    }

    [HttpPut("companies/{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] CompanyInput input)
    {
        if (!IsDeveloper()) return Forbid();
        if (!ModelState.IsValid || input.MaxUsers <= 0 || string.IsNullOrWhiteSpace(input.BusinessName))
            return BadRequest(new { error = "Datos inválidos." });
        var company = await db.Companies.FindAsync(id);
        if (company is null) return NotFound();
        if (await db.Companies.AnyAsync(c => c.Ruc == input.Ruc && c.Id != id))
            return Conflict(new { error = "RUC utilizado por otra empresa." });
        var users = await db.Users.CountAsync(u => u.CompanyId == id && u.IsActive);
        if (input.MaxUsers < users) return Conflict(new { error = "El límite de usuarios no puede ser inferior a los usuarios activos." });
        company.BusinessName = input.BusinessName.Trim(); company.Ruc = input.Ruc;
        company.PrimaryColorHex = input.PrimaryColorHex; company.LogoUrl = input.LogoUrl;
        var license = await db.CompanyLicenses.SingleOrDefaultAsync(l => l.CompanyId == id);
        if (license is null) { license = new CompanyLicense { CompanyId = id }; db.CompanyLicenses.Add(license); }
        license.Plan = input.Plan.Trim(); license.MaxUsers = input.MaxUsers;
        license.ExpiresAt = input.ExpiresAt.ToUniversalTime(); license.IsEnabled = input.LicenseEnabled;
        license.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(); return NoContent();
    }

    [HttpDelete("companies/{id:int}")]
    public async Task<IActionResult> Deactivate(int id)
    {
        if (!IsDeveloper()) return Forbid();
        var company = await db.Companies.FindAsync(id);
        if (company is null) return NotFound();
        // Nunca borrar en cascada. Se bloquea la baja si está referenciada.
        if (await db.Users.AnyAsync(u => u.CompanyId == id) ||
            await db.Inventories.AnyAsync(i => i.CompanyId == id) ||
            await db.Categories.AnyAsync(c => c.CompanyId == id) ||
            await db.Articles.AnyAsync(a => a.CompanyId == id) ||
            await db.Movements.AnyAsync(m => m.CompanyId == id))
            return Conflict(new { error = "La empresa tiene usuarios, inventarios u operaciones. Suspende su licencia si deseas bloquear el acceso." });
        company.IsActive = false; company.DeletionDate = DateTime.UtcNow;
        company.DeletionUser = User.FindFirst("UserId")?.Value;
        await db.SaveChangesAsync(); return NoContent();
    }
}
