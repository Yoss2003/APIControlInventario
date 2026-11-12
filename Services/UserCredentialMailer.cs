using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using InventoryAPI.Data;
using Microsoft.EntityFrameworkCore;

namespace InventoryAPI.Services;

/// <summary>Correo de bienvenida siguiendo la plantilla de MAUI; no registra ni devuelve claves al administrador.</summary>
public sealed class UserCredentialMailer(AppDbContext db, IConfiguration config, ILogger<UserCredentialMailer> logger)
{
    public static string NewTemporaryPassword() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(16));

    private sealed record SmtpConfig(string Host, int Port, string Sender, string Username, string Password);

    public async Task<bool> IsConfiguredAsync(int companyId)
        => await ConfigurationAsync(companyId) is not null;

    private async Task<SmtpConfig?> ConfigurationAsync(int companyId)
    {
        var host = config["PasswordRecovery:SmtpHost"];
        var sender = config["PasswordRecovery:SenderEmail"];
        var username = config["PasswordRecovery:SmtpUser"];
        var password = config["PasswordRecovery:SmtpPassword"];
        if (!string.IsNullOrWhiteSpace(host) && !string.IsNullOrWhiteSpace(sender) &&
            !string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
            return new SmtpConfig(host, int.TryParse(config["PasswordRecovery:SmtpPort"], out var port) ? port : 587,
                sender, username, password);

        var profile = await db.Profiles.AsNoTracking().Where(p =>
            (p.CompanyId == companyId || p.CompanyId == 1) &&
            p.SmtpEmail != null && p.SmtpPassword != null &&
            p.SmtpEmail != "" && p.SmtpPassword != "")
            .OrderByDescending(p => p.CompanyId == companyId).FirstOrDefaultAsync();
        return profile is null ? null : new SmtpConfig("smtp.gmail.com", 587,
            profile.SmtpEmail!, profile.SmtpEmail!, profile.SmtpPassword!);
    }

    public async Task<(bool Sent, string Message)> SendAsync(
        int companyId, string recipientEmail, string firstName, string username,
        string password)
    {
        var smtpConfig = await ConfigurationAsync(companyId);
        if (smtpConfig is null) return (false, "SMTP no está configurado; configura el remitente antes de registrar personal.");
        if (string.IsNullOrWhiteSpace(recipientEmail)) return (false, "El usuario debe tener un correo válido.");

        var safeName = WebUtility.HtmlEncode(firstName);
        var safeUser = WebUtility.HtmlEncode(username);
        var safePassword = WebUtility.HtmlEncode(password);
        var html = $"""
          <div style="font-family:Segoe UI,Arial,sans-serif;max-width:600px;margin:auto;padding:28px;
                      border:1px solid #dce4ea;border-radius:12px;background:#f9fbfd;color:#293845">
            <h2 style="text-align:center;color:#2474a6">Bienvenido a Control Inventario, {safeName}</h2>
            <p>Se ha creado tu cuenta de acceso. Estas son tus credenciales temporales:</p>
            <div style="border:1px solid #dce4ea;border-radius:9px;background:white;
                        padding:18px;text-align:center">
              <p><b>Usuario:</b> {safeUser}</p>
              <p><b>Contraseña temporal:</b> <code style="font-size:16px">{safePassword}</code></p>
            </div>
            <p style="padding:12px;border-left:4px solid #e4a63c;background:#fff4db">
              Cambia esta contraseña obligatoriamente al ingresar por primera vez.
              Nunca compartas tus credenciales. Si no solicitaste esta cuenta, contacta al administrador.
            </p>
            <p style="font-size:12px;color:#748390;text-align:center">Correo automático de Control Inventario.</p>
          </div>
          """;
        try
        {
            using var smtp = new SmtpClient(smtpConfig.Host, smtpConfig.Port)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(smtpConfig.Username, smtpConfig.Password),
                Timeout = 20000
            };
            using var mail = new MailMessage(new MailAddress(smtpConfig.Sender, "Control Inventario"),
                new MailAddress(recipientEmail.Trim()))
            {
                Subject = "Bienvenido a Control Inventario — credenciales temporales",
                Body = html,
                IsBodyHtml = true
            };
            await smtp.SendMailAsync(mail);
            return (true, "Se enviaron las credenciales al correo del usuario.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Fallo al enviar correo de alta del usuario en empresa {CompanyId}", companyId);
            return (false, "El usuario se creó, pero el correo no pudo enviarse. Verifica SMTP y usa Reenviar credenciales.");
        }
    }
}
