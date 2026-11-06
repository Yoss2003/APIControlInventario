using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using InventoryAPI.Security;
using InventoryAPI.Data;
using InventoryAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryAPI.Controllers;

[ApiController]
[Route("api/password-recovery")]
public sealed class PasswordRecoveryController(AppDbContext db, IConfiguration config) : ControllerBase
{
    public sealed record RequestDto(int CompanyId, [Required] string UsernameOrEmail);
    public sealed record CompleteDto([Required] string Token, [Required] string NewPassword);

    private bool Developer => User.FindFirst("RoleId")?.Value == "1" &&
        int.TryParse(User.FindFirst("UserId")?.Value, out var uid) && uid > 0;

    private static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    private async Task SendEmail(string to, string subject, string body)
    {
        var host = config["PasswordRecovery:SmtpHost"];
        var from = config["PasswordRecovery:SenderEmail"];
        var smtpUser = config["PasswordRecovery:SmtpUser"];
        var smtpPass = config["PasswordRecovery:SmtpPassword"];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from) ||
            string.IsNullOrWhiteSpace(smtpUser) || string.IsNullOrWhiteSpace(smtpPass))
            throw new InvalidOperationException("SMTP de recuperación no configurado.");
        using var smtp = new SmtpClient(host, int.TryParse(config["PasswordRecovery:SmtpPort"], out var port) ? port : 587)
        { EnableSsl = true, Credentials = new NetworkCredential(smtpUser, smtpPass) };
        using var message = new MailMessage(new MailAddress(from), new MailAddress(to))
        { Subject = subject, Body = body, IsBodyHtml = false };
        await smtp.SendMailAsync(message);
    }

    [AllowAnonymous]
    [HttpPost("request")]
    public async Task<IActionResult> RequestReset([FromBody] RequestDto input)
    {
        // Respuesta idéntica para cuentas existentes o inexistentes: no enumerar usuarios.
        var reply = new { message = "Si la cuenta existe, está activa y tiene correo, tu solicitud será revisada." };
        if (input.CompanyId <= 0 || string.IsNullOrWhiteSpace(input.UsernameOrEmail)) return Accepted(reply);
        var name = input.UsernameOrEmail.Trim();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u =>
            u.CompanyId == input.CompanyId && u.IsActive && u.StatusId == 1 &&
            (u.Username == name || u.Email == name));
        if (user is null || string.IsNullOrWhiteSpace(user.Email)) return Accepted(reply);
        var since = DateTime.UtcNow.AddMinutes(-15);
        if (await db.PasswordRecoveryRequests.AnyAsync(r => r.UserId == user.Id && r.RequestedAt >= since))
            return Accepted(reply);
        var request = new PasswordRecoveryRequest { CompanyId = input.CompanyId, UserId = user.Id };
        db.PasswordRecoveryRequests.Add(request); await db.SaveChangesAsync();
        var developerMail = config["PasswordRecovery:DeveloperEmail"];
        if (!string.IsNullOrWhiteSpace(developerMail))
        {
            try { await SendEmail(developerMail, "InventarioWeb: solicitud de recuperación",
                $"Solicitud #{request.Id} pendiente de revisión. Ingresa a Gestión Dev para aprobarla. No responda con contraseñas."); }
            catch { /* La solicitud persiste para revisión desde Gestión Dev; no divulgar SMTP al solicitante. */ }
        }
        return Accepted(reply);
    }

    [Authorize]
    [HttpGet("pending")]
    public async Task<IActionResult> Pending()
    {
        if (!Developer) return Forbid();
        return Ok(await db.PasswordRecoveryRequests.AsNoTracking()
            .Where(r => !r.Rejected && r.ApprovedAt == null && r.CompletedAt == null)
            .OrderByDescending(r => r.RequestedAt)
            .Select(r => new {
                r.Id, r.CompanyId, r.UserId, r.RequestedAt,
                username = db.Users.Where(u => u.Id == r.UserId).Select(u => u.Username).FirstOrDefault(),
                company = db.Companies.Where(c => c.Id == r.CompanyId).Select(c => c.BusinessName).FirstOrDefault()
            }).ToListAsync());
    }

    [Authorize]
    [HttpPost("{id:int}/approve")]
    public async Task<IActionResult> Approve(int id)
    {
        if (!Developer) return Forbid();
        var request = await db.PasswordRecoveryRequests.FirstOrDefaultAsync(r => r.Id == id);
        if (request is null || request.Rejected || request.ApprovedAt != null || request.CompletedAt != null)
            return Conflict(new { error = "Solicitud inexistente o ya procesada." });
        var target = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == request.UserId && u.CompanyId == request.CompanyId);
        if (target is null || !target.IsActive || string.IsNullOrWhiteSpace(target.Email))
            return Conflict(new { error = "El usuario no está activo o no tiene correo registrado." });
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var frontend = config["PasswordRecovery:FrontendBaseUrl"]?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(frontend) || !Uri.TryCreate(frontend, UriKind.Absolute, out var url) ||
            (url.Scheme != Uri.UriSchemeHttps && url.Host != "localhost"))
            return StatusCode(503, new { error = "Configura FrontendBaseUrl con HTTPS." });
        var link = frontend + "/reset-password?token=" + Uri.EscapeDataString(token);
        request.ApprovedAt = DateTime.UtcNow;
        request.ApprovedByUserId = int.Parse(User.FindFirst("UserId")!.Value);
        request.ExpiresAt = DateTime.UtcNow.AddMinutes(30);
        request.TokenHash = HashToken(token);
        await db.SaveChangesAsync();
        try
        {
            await SendEmail(target.Email, "Recuperación aprobada - InventarioWeb",
                $"Se aprobó tu solicitud. En los próximos 30 minutos, establece tu propia contraseña mediante este enlace de un solo uso:\n{link}\nSi no lo solicitaste, comunícate con el administrador.");
        }
        catch
        {
            request.ApprovedAt = null; request.ApprovedByUserId = null; request.ExpiresAt = null; request.TokenHash = null;
            await db.SaveChangesAsync();
            return StatusCode(503, new { error = "No fue posible enviar el correo. Revisa SMTP y vuelve a aprobar." });
        }
        return Ok(new { message = "Aprobado. Se envió un enlace temporal al correo registrado." });
    }

    [Authorize]
    [HttpPost("{id:int}/reject")]
    public async Task<IActionResult> Reject(int id)
    {
        if (!Developer) return Forbid();
        var r = await db.PasswordRecoveryRequests.FirstOrDefaultAsync(x => x.Id == id && x.ApprovedAt == null);
        if (r is null) return NotFound();
        r.Rejected = true; await db.SaveChangesAsync(); return NoContent();
    }

    [AllowAnonymous]
    [HttpPost("complete")]
    public async Task<IActionResult> Complete([FromBody] CompleteDto input)
    {
        if (string.IsNullOrWhiteSpace(input.Token) || input.Token.Length != 64 ||
            string.IsNullOrWhiteSpace(input.NewPassword) || input.NewPassword.Length < 12 || input.NewPassword.Length > 128)
            return BadRequest(new { error = "Enlace inválido o contraseña fuera de 12–128 caracteres." });
        var hash = HashToken(input.Token);
        await using var transaction = await db.Database.BeginTransactionAsync();
        var r = await db.PasswordRecoveryRequests.FromSqlInterpolated(
            $"SELECT * FROM \"PasswordRecoveryRequests\" WHERE \"TokenHash\" = {hash} FOR UPDATE")
            .SingleOrDefaultAsync();
        if (r is null || r.ApprovedAt is null || r.CompletedAt != null || r.ExpiresAt <= DateTime.UtcNow)
            return BadRequest(new { error = "Enlace inválido, utilizado o expirado." });
        var user = await db.Users.FromSqlInterpolated(
            $"SELECT * FROM \"Users\" WHERE \"Id\" = {r.UserId} FOR UPDATE").SingleOrDefaultAsync();
        if (user is null || !user.IsActive || user.CompanyId != r.CompanyId) return BadRequest(new { error = "Cuenta no disponible." });
        user.Password = PasswordHashService.HashPassword(input.NewPassword);
        user.MustChangePassword = false;
        r.CompletedAt = DateTime.UtcNow;
        r.TokenHash = null;
        // Invalida cualquier otro enlace aprobado o pendiente para esta cuenta.
        var others = await db.PasswordRecoveryRequests.Where(x => x.UserId == r.UserId && x.Id != r.Id && x.CompletedAt == null).ToListAsync();
        foreach (var other in others) { other.TokenHash = null; other.Rejected = true; }
        await db.SaveChangesAsync(); await transaction.CommitAsync();
        return Ok(new { message = "Contraseña actualizada. Ya puedes iniciar sesión." });
    }
}
