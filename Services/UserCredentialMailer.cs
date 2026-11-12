using System.Net;
using System.Net.Mail;
using System.Net.Sockets;
using System.Security.Authentication;
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
            <h2 style="text-align:center;color:#2474a6">Bienvenido a OmniStock, {safeName}</h2>
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
            <p style="font-size:12px;color:#748390;text-align:center">Correo automático de OmniStock.</p>
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
            using var mail = new MailMessage(new MailAddress(smtpConfig.Sender, "OmniStock"),
                new MailAddress(recipientEmail.Trim()))
            {
                Subject = "Bienvenido a OmniStock — credenciales temporales",
                Body = html,
                IsBodyHtml = true
            };
            await smtp.SendMailAsync(mail);
            return (true, "Se enviaron las credenciales al correo del usuario.");
        }
        catch (Exception ex)
        {
            // La causa detallada solo se registra en el servidor. Nunca enviar
            // nombres de usuario SMTP, contraseñas ni respuestas sin filtrar.
            logger.LogWarning(ex,
                "Fallo al enviar credenciales por SMTP en empresa {CompanyId} (tipo {ErrorType})",
                companyId, ex.GetType().Name);
            return (false, DescribeSmtpFailure(ex));
        }
    }

    private static string DescribeSmtpFailure(Exception error)
    {
        const string retry = " El usuario y su inventario sí se guardaron. Corrige SMTP y usa Reenviar credenciales.";
        if (error is SmtpFailedRecipientException)
            return "El servidor de correo rechazó al destinatario. Revisa que el correo sea correcto." + retry;

        if (error is SmtpException smtp)
        {
            var raw = smtp.Message ?? string.Empty;
            if (raw.Contains("5.7.8", StringComparison.OrdinalIgnoreCase) ||
                raw.Contains("5.7.9", StringComparison.OrdinalIgnoreCase) ||
                raw.Contains("authentication", StringComparison.OrdinalIgnoreCase) ||
                raw.Contains("credentials", StringComparison.OrdinalIgnoreCase))
                return "SMTP rechazó la autenticación. Verifica el remitente y la contraseña de aplicación." + retry;

            if (smtp.StatusCode is SmtpStatusCode.MustIssueStartTlsFirst)
                return "El servidor requiere TLS/STARTTLS. Revisa el host, puerto y seguridad SMTP." + retry;

            if (smtp.InnerException is SocketException or AuthenticationException ||
                smtp.StatusCode is SmtpStatusCode.ServiceNotAvailable)
                return "No se pudo conectar de forma segura al servidor SMTP. Revisa red, host y puerto." + retry;

            return "SMTP no confirmó el envío. Verifica el remitente, las credenciales y la configuración de correo." + retry;
        }

        if (error is SocketException or AuthenticationException)
            return "No se pudo establecer una conexión segura con SMTP. Revisa host, puerto y red." + retry;

        return "No se confirmó el envío del correo por SMTP. Revisa los registros de la API." + retry;

    }
}
