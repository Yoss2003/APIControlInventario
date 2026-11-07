using InventoryAPI.Data;
using InventoryAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryAPI.Controllers;

/// <summary>
/// Los logos públicos se consultan por ID; solo un desarrollador autenticado puede sustituirlos.
/// El contenido binario se almacena en PostgreSQL (no en el filesystem temporal de Render).
/// </summary>
[ApiController]
[Route("api/company-logos")]
public sealed class CompanyLogosController(AppDbContext db) : ControllerBase
{
    private const int MaxBytes = 2 * 1024 * 1024;

    [HttpGet("{companyId:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> Get(int companyId, CancellationToken ct)
    {
        if (companyId <= 0) return NotFound();
        var logo = await db.CompanyLogos.AsNoTracking()
            .Where(x => x.CompanyId == companyId &&
                db.Companies.Any(c => c.Id == companyId && c.IsActive))
            .Select(x => new { x.Content, x.ContentType })
            .FirstOrDefaultAsync(ct);
        if (logo is null) return NotFound();
        Response.Headers.CacheControl = "public, max-age=300";
        return File(logo.Content, logo.ContentType);
    }

    [HttpPost("{companyId:int}")]
    [Authorize]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxBytes + 1024 * 128)]
    public async Task<IActionResult> Upload(int companyId, IFormFile? file, CancellationToken ct)
    {
        if (User.FindFirst("RoleId")?.Value != "1" ||
            !int.TryParse(User.FindFirst("UserId")?.Value, out var uid) || uid <= 0)
            return Forbid();
        if (file is null || file.Length == 0 || file.Length > MaxBytes)
            return BadRequest(new { error = "Selecciona una imagen PNG, JPG o WebP de hasta 2 MB." });
        var company = await db.Companies.FirstOrDefaultAsync(x => x.Id == companyId, ct);
        if (company is null) return NotFound();
        if (!company.IsActive)
            return Conflict(new { error = "No se permite cambiar el logo de una empresa inactiva." });

        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream, ct);
        var bytes = stream.ToArray();
        var detected = DetectImage(bytes);
        if (detected is null || !string.Equals(file.ContentType, detected, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "El archivo no contiene una imagen PNG, JPG o WebP válida." });

        var logo = await db.CompanyLogos.SingleOrDefaultAsync(x => x.CompanyId == companyId, ct);
        if (logo is null)
        {
            logo = new CompanyLogo { CompanyId = companyId };
            db.CompanyLogos.Add(logo);
        }
        logo.Content = bytes;
        logo.ContentType = detected;
        logo.UpdatedAt = DateTime.UtcNow;
        // Query de versión para evitar que el navegador muestre el logo anterior en caché.
        company.LogoUrl = $"/api/company-logos/{companyId}?v={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
        await db.SaveChangesAsync(ct);
        return Ok(new { logoUrl = company.LogoUrl });
    }

    private static string? DetectImage(byte[] data)
    {
        if (data.Length >= 8 && data[0] == 0x89 && data[1] == 0x50 &&
            data[2] == 0x4E && data[3] == 0x47 && data[4] == 0x0D &&
            data[5] == 0x0A && data[6] == 0x1A && data[7] == 0x0A)
            return "image/png";
        if (data.Length >= 4 && data[0] == 0xFF && data[1] == 0xD8 &&
            data[2] == 0xFF && data[^2] == 0xFF && data[^1] == 0xD9)
            return "image/jpeg";
        if (data.Length >= 12 && data[0] == (byte)'R' && data[1] == (byte)'I' &&
            data[2] == (byte)'F' && data[3] == (byte)'F' &&
            data[8] == (byte)'W' && data[9] == (byte)'E' &&
            data[10] == (byte)'B' && data[11] == (byte)'P')
            return "image/webp";
        return null;
    }
}
