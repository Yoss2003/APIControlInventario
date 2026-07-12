using ControlInventario.Shared.Models;
using InventoryAPI.Data;
using InventoryAPI.Models.DTO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OtpNet;
using System.Diagnostics;
using System.Net;
using System.Net.Mail;

namespace InventoryAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        public UsersController(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // GET: api/Users
        [HttpGet]
        public async Task<ActionResult<IEnumerable<UserDTO>>> GetUsers()
        {
            return await _context.Users
                .Include(u => u.Role)
                .Include(u => u.Employee)
                .Select(u => new UserDTO
                {
                    Id = u.Id,
                    FirstName = u.Employee!.FirstName ?? "",
                    LastName = u.Employee!.LastName ?? "",
                    Email = u.Email ?? "",
                    Username = u.Username!,
                    Age = u.Employee!.Age ?? 0,
                    BirthDate = u.Employee!.BirthDate ?? "",
                    HireDate = u.Employee.HireDate ?? "",
                    PhoneNumber = u.PhoneNumber ?? "",
                    ProfilePictureUrl = u.ProfilePictureUrl ?? "",
                    IsActive = u.IsActive,
                    RoleName = u.Role!.Name ?? "Usuario",
                    JobPositionId = u.Employee.JobPositionId ?? 0,
                    AreaId = u.Employee.AreaId ?? 0,
                    ContractTypeId = u.Employee.ContractTypeId ?? 0,
                    RoleId = u.RoleId
                })
                .ToListAsync();
        }

        // GET: api/Users/5
        [HttpGet("{id}")]
        public async Task<ActionResult<UserDTO>> GetUser(int id)
        {
            var userDto = await _context.Users
                .Include(u => u.Role)
                .Include(u => u.Employee)
                .Where(u => u.Id == id)
                .Select(u => new UserDTO
                {
                    Id = u.Id,
                    FirstName = u.Employee!.FirstName ?? "",
                    LastName = u.Employee.LastName ?? "",
                    Email = u.Email ?? "",
                    Username = u.Username!,
                    Age = u.Employee.Age ?? 0,
                    BirthDate = u.Employee.BirthDate ?? "",
                    HireDate = u.Employee.HireDate ?? "",
                    PhoneNumber = u.PhoneNumber ?? "",
                    ProfilePictureUrl = u.ProfilePictureUrl ?? "",
                    IsActive = u.IsActive,
                    RoleName = u.Role!.Name ?? "Usuario",
                    JobPositionId = u.Employee.JobPositionId ?? 0,
                    AreaId = u.Employee.AreaId ?? 0,
                    ContractTypeId = u.Employee.ContractTypeId ?? 0,
                    RoleId = u.RoleId
                })

                .FirstOrDefaultAsync();

            if (userDto == null) return NotFound(new { mensaje = "El usuario no fue localizado." });

            return userDto;
        }

        // PUT: api/Users/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutUser(int id, [FromBody] User userActualizado)
        {
            if (id != userActualizado.Id) return BadRequest(new { mensaje = "El ID no coincide." });

            var userDb = await _context.Users
                .Include(u => u.Role)
                .Include(u => u.Employee)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (userDb == null) return NotFound(new { mensaje = "El usuario no existe." });

            userDb.Email = userActualizado.Email;
            userDb.Username = userActualizado.Username;
            userDb.PhoneNumber = userActualizado.PhoneNumber;
            userDb.ProfilePictureUrl = userActualizado.ProfilePictureUrl;
            userDb.IsActive = userActualizado.IsActive;

            if (userActualizado.RoleId > 0) userDb.RoleId = userActualizado.RoleId;
            if (!string.IsNullOrWhiteSpace(userActualizado.Password)) userDb.Password = userActualizado.Password;
            userDb.MustChangePassword = userActualizado.MustChangePassword;

            if (userActualizado.Employee != null)
            {
                if (userDb.Employee == null) userDb.Employee = new Employee();

                userDb.Employee.FirstName = userActualizado.Employee.FirstName;
                userDb.Employee.LastName = userActualizado.Employee.LastName;

                if (userActualizado.Employee.AreaId > 0) userDb.Employee.AreaId = userActualizado.Employee.AreaId;
                if (userActualizado.Employee.JobPositionId > 0) userDb.Employee.JobPositionId = userActualizado.Employee.JobPositionId;
                if (userActualizado.Employee.ContractTypeId > 0) userDb.Employee.ContractTypeId = userActualizado.Employee.ContractTypeId;
            }

            try
            {
                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Error al editar", detalle = ex.InnerException?.Message ?? ex.Message });
            }
        }

        // POST: api/Users
        [HttpPost]
        public async Task<ActionResult<User>> PostUser(User user)
        {
            var perfilConfigurado = await _context.Profiles
                .FirstOrDefaultAsync(p => !string.IsNullOrEmpty(p.SmtpEmail) && !string.IsNullOrEmpty(p.SmtpPassword));

            if (perfilConfigurado == null)
            {
                return BadRequest(new
                {
                    requiresSmtpConfiguration = true,
                    mensaje = "El sistema requiere que configures el correo emisario (SMTP) en los Ajustes antes de registrar personal."
                });
            }

            if (user.Employee == null) user.Employee = new Employee();

            user.Employee.JobPositionId = (user.Employee.JobPositionId == null || user.Employee.JobPositionId <= 0) ? 1 : user.Employee.JobPositionId;
            user.Employee.AreaId = (user.Employee.AreaId == null || user.Employee.AreaId <= 0) ? 1 : user.Employee.AreaId;
            user.Employee.ContractTypeId = (user.Employee.ContractTypeId == null || user.Employee.ContractTypeId <= 0) ? 1 : user.Employee.ContractTypeId;
            user.Employee.Age = user.Employee.Age ?? 0;
            user.IsActive = true;
            user.StatusId = 2;
            user.Employee.StatusId = 2;
            user.Role = null;

            string clavePlana = user.Password!;

            if (!string.IsNullOrEmpty(user.ProfilePictureUrl) && user.ProfilePictureUrl.Length > 500)
            {
                try
                {
                    // 1. Limpiar el Base64 (Por si MAUI o el serializador le agregó basura al inicio)
                    string base64Data = user.ProfilePictureUrl;
                    if (base64Data.Contains(","))
                    {
                        base64Data = base64Data.Substring(base64Data.IndexOf(",") + 1);
                    }

                    // 2. Usar ContentRootPath es 100% más seguro en Somee que GetCurrentDirectory
                    string basePath = _env.ContentRootPath;
                    string uploadsFolder = Path.Combine(basePath, "wwwroot", "images", "profiles");

                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    string uniqueFileName = Guid.NewGuid().ToString() + ".jpg";
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    byte[] imageBytes = Convert.FromBase64String(base64Data);
                    await System.IO.File.WriteAllBytesAsync(filePath, imageBytes);

                    user.ProfilePictureUrl = $"http://db-inventario-api.somee.com/images/profiles/{uniqueFileName}";
                }
                catch (Exception ex)
                {
                    // 🚨 FORZAMOS EL ERROR: Si Somee falla, paramos la creación y te mostramos por qué
                    return BadRequest(new
                    {
                        mensaje = "Fallo al guardar la imagen en el servidor de Somee.",
                        detalle = ex.Message,
                        inner = ex.InnerException?.Message
                    });
                }
            }

            try
            {
                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                string fechaActual = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
                string prefijoFecha = DateTime.Now.ToString("ddMM");

                var nuevoInventario = new Inventory
                {
                    InventoryName = $"{user.Username}_Invent_{prefijoFecha}",
                    CreationDate = fechaActual,
                    ModificationDate = fechaActual,
                    UserId = user.Id,
                    Username = user.Username!,
                    Alias = "Inventario Principal"
                };

                _context.Inventories.Add(nuevoInventario);
                await _context.SaveChangesAsync();

                string nombreFiltro = $"{user.Employee.FirstName} {user.Employee.LastName}".Trim();
                _ = EnviarCorreoAprobacionAsync(user, nombreFiltro, perfilConfigurado.SmtpEmail!, perfilConfigurado.SmtpPassword!, perfilConfigurado.SmtpApproverEmail!);
                _ = EnviarCorreoBienvenidaAsync(user, clavePlana, perfilConfigurado.SmtpEmail!, perfilConfigurado.SmtpPassword!);

                return CreatedAtAction("GetUser", new { id = user.Id }, user);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CRÍTICO SQL]: {ex.InnerException?.Message ?? ex.Message}");
                return StatusCode(500, new { error = "Fallo de Inserción en BD", detalle = ex.InnerException?.Message ?? ex.Message });
            }
        }

        // ==========================================
        // MÉTODOS DE 2FA Y FOTO (Intactos)
        // ==========================================

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpPost("Login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDTO request)
        {
            var user = await _context.Users
                .Include(u => u.Role)
                .ThenInclude(r => r!.RolePermissions!)
                .ThenInclude(rp => rp!.Permission)
                .Include(u => u.Employee)
                .FirstOrDefaultAsync(u => u.Username == request.Username && u.Password == request.Password);

            if (user == null) return Unauthorized(new { mensaje = "Usuario o contraseña incorrectos" });

            if (!user.IsActive || user.StatusId != 1) return Unauthorized(new { accountPending = true, mensaje = "Tu cuenta se encuentra inactiva o pendiente de validación por parte de Gerencia." });

            // Tu código intacto de 2FA
            if (user.IsTwoFactorEnabled)
            {
                if (string.IsNullOrWhiteSpace(request.TwoFactorCode)) return Unauthorized(new { requires2FA = true, mensaje = "Código 2FA requerido" });

                var secretBytes = Base32Encoding.ToBytes(user.TwoFactorSecret);
                var totp = new Totp(secretBytes);
                bool isValid = totp.VerifyTotp(request.TwoFactorCode, out long timeStepMatched, window: new VerificationWindow(2, 2));

                if (!isValid) return Unauthorized(new { mensaje = "El código de seguridad es incorrecto o ha expirado." });
            }

            if (user.MustChangePassword)
            {
                return Ok(new
                {
                    requirePasswordChange = true,
                    user
                });
            }

            return Ok(user);
        }

        [HttpPost("ChangeInitialPassword")]
        public async Task<IActionResult> ChangeInitialPassword([FromBody] ChangePasswordRequest request)
        {
            var user = await _context.Users.FindAsync(request.UserId);

            if (user == null)
                return NotFound(new { mensaje = "Usuario no encontrado." });

            user.Password = request.NewPassword;
            user.MustChangePassword = false;

            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            return Ok(user);
        }

        [HttpPut("{id}/UpdatePhoto")]
        public async Task<IActionResult> UpdatePhoto(int id, [FromBody] PhotoUpdateDTO request)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound(new { mensaje = "Usuario no encontrado." });

            if (!string.IsNullOrEmpty(request.Base64Image))
            {
                try
                {
                    string base64Data = request.Base64Image;
                    if (base64Data.Contains(","))
                        base64Data = base64Data.Substring(base64Data.IndexOf(",") + 1);

                    string basePath = _env.ContentRootPath;
                    string uploadsFolder = Path.Combine(basePath, "wwwroot", "images", "profiles");
                    if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                    string uniqueFileName = Guid.NewGuid().ToString() + ".jpg";
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    byte[] imageBytes = Convert.FromBase64String(base64Data);
                    await System.IO.File.WriteAllBytesAsync(filePath, imageBytes);

                    // 🚨 GUARDAMOS LA URL EN LA BASE DE DATOS
                    user.ProfilePictureUrl = $"http://db-inventario-api.somee.com/images/profiles/{uniqueFileName}";
                    await _context.SaveChangesAsync();

                    return Ok(new { Url = user.ProfilePictureUrl, mensaje = "Foto actualizada correctamente." });
                }
                catch (Exception ex)
                {
                    return StatusCode(500, new { mensaje = "Fallo al guardar imagen en Somee.", detalle = ex.Message });
                }
            }

            return BadRequest(new { mensaje = "No se envió ninguna imagen." });
        }

        [HttpPost("{id}/generate-2fa")]
        public async Task<IActionResult> Generate2FA(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();
            var key = KeyGeneration.GenerateRandomKey(20);
            var secret = Base32Encoding.ToString(key);
            user.TwoFactorSecret = secret;
            await _context.SaveChangesAsync();
            var qrUri = $"otpauth://totp/ControlInventario:{user.Username}?secret={secret}&issuer=ControlInventarioCorp";
            return Ok(new { secret, qrUri });
        }

        [HttpPost("{id}/enable-2fa")]
        public async Task<IActionResult> Enable2FA(int id, [FromBody] string code)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null || string.IsNullOrEmpty(user.TwoFactorSecret)) return NotFound();
            var totp = new Totp(Base32Encoding.ToBytes(user.TwoFactorSecret));
            if (totp.VerifyTotp(code, out _, window: new VerificationWindow(2, 2)))
            {
                user.IsTwoFactorEnabled = true;
                await _context.SaveChangesAsync();
                return Ok(new { mensaje = "Activado" });
            }
            return BadRequest(new { mensaje = "Inválido" });
        }

        [HttpPost("{id}/disable-2fa")]
        public async Task<IActionResult> Disable2FA(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();
            user.IsTwoFactorEnabled = false;
            user.TwoFactorSecret = null;
            await _context.SaveChangesAsync();
            return Ok(new { mensaje = "Desactivado" });
        }

        private async Task EnviarCorreoAprobacionAsync(User user, string nombreCompleto, string remitente, string passwordApp, string approverEmail)
        {
            try
            {
                var smtpClient = new SmtpClient("smtp.gmail.com")
                {
                    Port = 587,
                    Credentials = new NetworkCredential(remitente, passwordApp),
                    EnableSsl = true,
                };

                string linkAprobacion = $"http://db-inventario-api.somee.com/api/Users/Approve/{user.Id}";

                string htmlBody = $@"<div style='font-family: Arial, sans-serif; max-width: 600px; margin: auto; border: 1px solid #ddd; padding: 20px; border-radius: 10px; background-color: #ffffff; color: #333333;'><h2 style='color: #E74C3C; text-align: center;'>⚠️ Aprobación de Personal Requerida</h2><p>Hola,</p><p>Se ha registrado un nuevo empleado de emergencia en la plataforma de inventarios utilizando tus credenciales de emisor.</p><hr style='border: none; border-top: 1px solid #eee;' /><p><b>Detalles del registro:</b></p><ul style='list-style: none; padding-left: 0;'><li style='margin-bottom: 5px;'><b>Nombre del Colaborador:</b> {nombreCompleto}</li><li style='margin-bottom: 5px;'><b>Nombre de Usuario:</b> {user.Username}</li><li style='margin-bottom: 5px;'><b>Fecha/Hora de Alta:</b> {DateTime.Now:dd/MM/yyyy HH:mm}</li></ul><hr style='border: none; border-top: 1px solid #eee;' /><p>La cuenta se encuentra actualmente en estado <b>Pendiente de Validación (Estatus 2)</b>. De acuerdo con las políticas del sistema, cuentas con un plazo máximo de <b>48 horas</b> para confirmar su acceso definitivo antes de que sea suspendida automáticamente.</p><br/><div style='text-align: center;'><a href='{linkAprobacion}' style='background-color: #2ECC71; color: white; padding: 14px 30px; text-decoration: none; font-weight: bold; border-radius: 5px; font-size: 16px; display: inline-block; box-shadow: 0 4px 6px rgba(0,0,0,0.1);'>Validar y Activar Empleado</a></div><br/><p style='font-size: 12px; color: #7f8c8d; text-align: center;'>Este es un correo automático generado por el módulo de seguridad del Sistema de Inventario.</p></div>";

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(remitente, "Control Inventario - Seguridad"),
                    Subject = "URGENTE: Validación de nuevo empleado requerida",
                    Body = htmlBody,
                    IsBodyHtml = true,
                };

                string destinatarioFinal = !string.IsNullOrWhiteSpace(approverEmail)
                    ? approverEmail.Trim()
                    : "mercadogarciaalexandro10@gmail.com";

                mailMessage.To.Add(destinatarioFinal);

                await smtpClient.SendMailAsync(mailMessage);
                Debug.WriteLine($"[EMAIL SUCCESS]: Aprobación enviada a {destinatarioFinal}.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[EMAIL CRITICAL ERROR]: Fallo al procesar el correo de aprobación. Detalle: {ex.Message}");
            }
        }

        private async Task EnviarCorreoBienvenidaAsync(User user, string clavePlana, string remitente, string passwordApp)
        {
            try
            {
                // Si no le asignaron correo al empleado, no podemos enviarle la bienvenida
                if (string.IsNullOrWhiteSpace(user.Email)) return;

                var smtpClient = new SmtpClient("smtp.gmail.com")
                {
                    Port = 587,
                    Credentials = new NetworkCredential(remitente, passwordApp),
                    EnableSsl = true,
                };

                string htmlBody = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: auto; border: 1px solid #ddd; padding: 20px; border-radius: 10px; background-color: #f9f9f9; color: #333333;'>
                    <h2 style='color: #2980b9; text-align: center;'>¡Bienvenido al Equipo!</h2>
                    <p>Hola <b>{user.Employee?.FirstName}</b>,</p>
                    <p>Tu cuenta corporativa en el Sistema de Inventario ha sido creada exitosamente. Actualmente se encuentra en proceso de validación por parte de Gerencia.</p>
                    <hr style='border: none; border-top: 1px solid #ccc;' />
                    <p>Tus credenciales de acceso son las siguientes:</p>
                    <div style='background-color: #e8f4f8; padding: 15px; border-radius: 5px; text-align: center; font-size: 16px;'>
                        <p><b>Usuario:</b> {user.Username}</p>
                        <p><b>Contraseña:</b> <span style='font-family: monospace; color: #d35400;'>{clavePlana}</span></p>
                    </div>
                    <hr style='border: none; border-top: 1px solid #ccc;' />
                    <p style='color: #c0392b; font-size: 13px;'><b>Nota de Seguridad:</b> Al iniciar sesión por primera vez, el sistema te obligará a cambiar esta contraseña temporal por una personal.</p>
                    <br/>
                    <p style='font-size: 12px; color: #7f8c8d; text-align: center;'>Por favor, guarda este correo en un lugar seguro y no compartas tus credenciales.</p>
                </div>";

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(remitente, "Control Inventario - RRHH"),
                    Subject = "Bienvenido - Tus credenciales de acceso",
                    Body = htmlBody,
                    IsBodyHtml = true,
                };

                mailMessage.To.Add(user.Email.Trim());

                await smtpClient.SendMailAsync(mailMessage);
                Debug.WriteLine($"[EMAIL SUCCESS]: Bienvenida enviada al empleado {user.Email}.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[EMAIL CRITICAL ERROR]: Fallo al enviar bienvenida al empleado. Detalle: {ex.Message}");
            }
        }

        // GET: api/Users/Approve/5
        [HttpGet("Approve/{id}")]
        public async Task<IActionResult> ApproveEmployee(int id)
        {
            var user = await _context.Users.Include(u => u.Employee).FirstOrDefaultAsync(u => u.Id == id);

            if (user == null)
                return Content("<h1>Error: Usuario no encontrado.</h1>", "text/html", System.Text.Encoding.UTF8);

            user.StatusId = 1;

            _context.Users.Update(user);
            if (user.Employee != null)
            {
                _context.Employees.Update(user.Employee);
            }

            await _context.SaveChangesAsync();

            // 2️⃣ FIX BUG 2: Disparamos el correo informativo al Empleado
            var configuracion = await _context.Profiles.FirstOrDefaultAsync(p => !string.IsNullOrEmpty(p.SmtpEmail));
            if (configuracion != null && !string.IsNullOrEmpty(user.Email))
            {
                _ = EnviarCorreoActivacionExitosaAsync(user, configuracion.SmtpEmail!, configuracion.SmtpPassword!);
            }

            // 3️⃣ FIX BUG 3: HTML con <meta charset='UTF-8'> para que las tildes se vean perfectas
            string htmlBody = @"
            <!DOCTYPE html>
            <html lang='es'>
            <head>
                <meta charset='UTF-8'>
                <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                <title>Aprobación Exitosa</title>
            </head>
            <body style='text-align: center; font-family: Arial, sans-serif; padding: 50px; background-color: #f4f6f7;'>
                <h1 style='color: #2ECC71;'>✔️ ¡Aprobación Exitosa!</h1>
                <p style='font-size: 18px; color: #333;'>El colaborador ha sido validado permanentemente en el sistema.</p>
                <p style='color: #7f8c8d;'>Ya puedes cerrar esta ventana.</p>
            </body>
            </html>";

            // Retornamos especificando que es UTF8
            return Content(htmlBody, "text/html", System.Text.Encoding.UTF8);
        }

        private async Task EnviarCorreoActivacionExitosaAsync(User user, string remitente, string passwordApp)
        {
            try
            {
                var smtpClient = new System.Net.Mail.SmtpClient("smtp.gmail.com")
                {
                    Port = 587,
                    Credentials = new System.Net.NetworkCredential(remitente, passwordApp),
                    EnableSsl = true,
                };

                string htmlBody = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: auto; border: 1px solid #ddd; padding: 20px; border-radius: 10px; text-align: center;'>
                    <h2 style='color: #2ECC71;'>¡Tu cuenta ha sido activada! 🎉</h2>
                    <p>Hola <b>{user.Employee?.FirstName}</b>,</p>
                    <p>Nos complace informarte que Gerencia ha aprobado tu perfil.</p>
                    <p>Ya puedes abrir la aplicación móvil e iniciar sesión de forma normal utilizando tus credenciales.</p>
                    <br/>
                    <p style='font-size: 12px; color: #7f8c8d;'>Bienvenido al equipo.</p>
                </div>";

                var mailMessage = new System.Net.Mail.MailMessage
                {
                    From = new System.Net.Mail.MailAddress(remitente, "Control Inventario - RRHH"),
                    Subject = "✅ ¡Cuenta Activada! Ya puedes ingresar",
                    Body = htmlBody,
                    IsBodyHtml = true,
                };

                mailMessage.To.Add(user.Email!.Trim());

                await smtpClient.SendMailAsync(mailMessage);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EMAIL ERROR]: No se pudo avisar al empleado de su activación. Detalle: {ex.Message}");
            }
        }

        [HttpPost("TestEmailConfiguration")]
        public async Task<IActionResult> TestEmailConfiguration([FromBody] SmtpTestRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                return BadRequest(new { mensaje = "El correo y la contraseña son obligatorios." });

            try
            {
                var smtpClient = new SmtpClient("smtp.gmail.com")
                {
                    Port = 587,
                    Credentials = new NetworkCredential(request.Email, request.Password),
                    EnableSsl = true,
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(request.Email, "Control Inventario - Test"),
                    Subject = "✅ Prueba de Conexión Exitosa",
                    Body = @"
                <div style='font-family: Arial; padding: 20px; border: 1px solid #ddd; border-radius: 10px; text-align: center;'>
                    <h2 style='color: #2ECC71;'>¡Conexión Exitosa!</h2>
                    <p>Si estás leyendo este correo, significa que el sistema ya tiene acceso y autorización para enviar alertas y aprobaciones automáticas usando esta cuenta.</p>
                </div>",
                    IsBodyHtml = true,
                };

                // El sistema se enviará el correo a sí mismo para probar
                mailMessage.To.Add(request.Email);

                await smtpClient.SendMailAsync(mailMessage);
                return Ok(new { mensaje = "Correo enviado con éxito." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Fallo de conexión", detalle = ex.InnerException?.Message ?? ex.Message });
            }
        }
    }

    public class SmtpTestRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
    public class LoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
    public class PhotoUpdateDTO
    {
        public string Base64Image { get; set; } = string.Empty;
    }
}