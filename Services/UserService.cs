using ControlInventario.Shared.Models;
using InventoryAPI.Models.DTO;
using InventoryAPI.Repositories;
using InventoryAPI.Repositories.IRepositories;
using InventoryAPI.Services.IServices;
using OtpNet;
using System.Diagnostics;
using System.Net;
using System.Net.Mail;

namespace InventoryAPI.Services
{
    public class UserService(IWorkFlow workFlow) : WorkContainer<User>(workFlow), IUserService
    {
        public async Task<IEnumerable<UserDTO>> GetUsersDtoAsync()
        {
            var users = await _workFlow.Repository<User>().GetAllWithIncludeAsync(u => u.Role!, u => u.Employee!);
            return users.Select(u => MapToDto(u)).ToList();
        }

        public async Task<UserDTO?> GetUserDtoByIdAsync(int id)
        {
            var userMatch = await _workFlow.Repository<User>().FindAsync(u => u.Id == id);
            var user = userMatch.FirstOrDefault();
            if (user == null) return null;

            // Recargamos con relaciones si es necesario
            var fullUserMatch = await _workFlow.Repository<User>().GetAllWithIncludeAsync(u => u.Role!, u => u.Employee!);
            var fullUser = fullUserMatch.FirstOrDefault(u => u.Id == id) ?? user;

            return MapToDto(fullUser);
        }

        private UserDTO MapToDto(User u)
        {
            return new UserDTO
            {
                Id = u.Id,
                FirstName = u.Employee?.FirstName ?? "",
                LastName = u.Employee?.LastName ?? "",
                Email = u.Email ?? "",
                Username = u.Username ?? "",
                Age = u.Employee?.Age ?? 0,
                BirthDate = u.Employee?.BirthDate ?? "",
                HireDate = u.Employee?.HireDate ?? "",
                PhoneNumber = u.PhoneNumber ?? "",
                ProfilePictureUrl = u.ProfilePictureUrl ?? "",
                IsActive = u.IsActive,
                RoleName = u.Role?.Name ?? "Usuario",
                JobPositionId = u.Employee?.JobPositionId ?? 0,
                AreaId = u.Employee?.AreaId ?? 0,
                ContractTypeId = u.Employee?.ContractTypeId ?? 0,
                RoleId = u.RoleId
            };
        }

        public async Task<(bool Success, string Message)> UpdateUserAsync(int id, User userActualizado)
        {
            if (id != userActualizado.Id) return (false, "El ID no coincide.");

            var usersMatch = await _workFlow.Repository<User>().GetAllWithIncludeAsync(u => u.Role!, u => u.Employee!);
            var userDb = usersMatch.FirstOrDefault(u => u.Id == id);

            if (userDb == null) return (false, "El usuario no existe.");

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
                await _workFlow.CompleteAsync();
                return (true, "Actualizado con éxito.");
            }
            catch (Exception ex)
            {
                return (false, ex.InnerException?.Message ?? ex.Message);
            }
        }

        public async Task<(bool Success, object Data, string Message)> CreateUserAsync(User user, string contentRootPath)
        {
            var perfiles = await _workFlow.Repository<Profile>().GetAllAsync();
            var perfilConfigurado = perfiles.FirstOrDefault(p => !string.IsNullOrEmpty(p.SmtpEmail) && !string.IsNullOrEmpty(p.SmtpPassword));

            if (perfilConfigurado == null)
            {
                return (false, new { requiresSmtpConfiguration = true, mensaje = "El sistema requiere que configures el correo emisario (SMTP) en los Ajustes antes de registrar personal." }, "SMTP no configurado.");
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
                    string base64Data = user.ProfilePictureUrl;
                    if (base64Data.Contains(","))
                    {
                        base64Data = base64Data.Substring(base64Data.IndexOf(",") + 1);
                    }

                    string uploadsFolder = Path.Combine(contentRootPath, "wwwroot", "images", "profiles");
                    if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                    string uniqueFileName = Guid.NewGuid().ToString() + ".jpg";
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    byte[] imageBytes = Convert.FromBase64String(base64Data);
                    await File.WriteAllBytesAsync(filePath, imageBytes);

                    user.ProfilePictureUrl = $"http://db-inventario-api.somee.com/images/profiles/{uniqueFileName}";
                }
                catch (Exception ex)
                {
                    return (false, new { mensaje = "Fallo al guardar la imagen en el servidor de Somee.", detalle = ex.Message }, ex.Message);
                }
            }

            try
            {
                await _workFlow.Repository<User>().AddAsync(user);
                await _workFlow.CompleteAsync();

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

                await _workFlow.Repository<Inventory>().AddAsync(nuevoInventario);
                await _workFlow.CompleteAsync();

                string nombreFiltro = $"{user.Employee.FirstName} {user.Employee.LastName}".Trim();
                _ = EnviarCorreoAprobacionAsync(user, nombreFiltro, perfilConfigurado.SmtpEmail!, perfilConfigurado.SmtpPassword!, perfilConfigurado.SmtpApproverEmail!);
                _ = EnviarCorreoBienvenidaAsync(user, clavePlana, perfilConfigurado.SmtpEmail!, perfilConfigurado.SmtpPassword!);

                return (true, user, "Usuario creado exitosamente.");
            }
            catch (Exception ex)
            {
                return (false, ex.InnerException?.Message ?? ex.Message, "Error crítico SQL.");
            }
        }

        public async Task<(bool Success, User? User, string Message, bool Requires2FA, bool RequirePasswordChange, bool AccountPending)> LoginAsync(LoginRequestDTO request)
        {
            var usersMatch = await _workFlow.Repository<User>().GetAllWithIncludeAsync(
                u => u.Role!,
                u => u.Employee!
            );

            // Si necesitamos incluir los RolePermissions en cascada
            var allUsers = await _workFlow.Repository<User>().GetAllAsync();
            var targetUser = allUsers.FirstOrDefault(u => u.Username == request.Username && u.Password == request.Password);

            if (targetUser == null) return (false, null, "Usuario o contraseña incorrectos", false, false, false);

            if (!targetUser.IsActive || targetUser.StatusId != 1)
                return (false, null, "Tu cuenta se encuentra inactiva o pendiente de validación por parte de Gerencia.", false, false, true);

            if (targetUser.IsTwoFactorEnabled)
            {
                if (string.IsNullOrWhiteSpace(request.TwoFactorCode))
                    return (false, null, "Código 2FA requerido", true, false, false);

                var secretBytes = Base32Encoding.ToBytes(targetUser.TwoFactorSecret);
                var totp = new Totp(secretBytes);
                bool isValid = totp.VerifyTotp(request.TwoFactorCode, out _, window: new VerificationWindow(2, 2));

                if (!isValid) return (false, null, "El código de seguridad es incorrecto o ha expirado.", false, false, false);
            }

            if (targetUser.MustChangePassword)
            {
                return (true, targetUser, "Debe cambiar contraseña", false, true, false);
            }

            return (true, targetUser, "Login exitoso", false, false, false);
        }

        public async Task<(bool Success, User? User, string Message)> ChangeInitialPasswordAsync(int userId, string newPassword)
        {
            var user = await _workFlow.Repository<User>().GetByIdAsync(userId);
            if (user == null) return (false, null, "Usuario no encontrado.");

            user.Password = newPassword;
            user.MustChangePassword = false;

            await _workFlow.CompleteAsync();
            return (true, user, "Contraseña cambiada con éxito.");
        }

        public async Task<(bool Success, string Url, string Message)> UpdatePhotoAsync(int id, string base64Image, string contentRootPath)
        {
            var user = await _workFlow.Repository<User>().GetByIdAsync(id);
            if (user == null) return (false, "", "Usuario no encontrado.");

            if (!string.IsNullOrEmpty(base64Image))
            {
                try
                {
                    string base64Data = base64Image;
                    if (base64Data.Contains(","))
                        base64Data = base64Data.Substring(base64Data.IndexOf(",") + 1);

                    string uploadsFolder = Path.Combine(contentRootPath, "wwwroot", "images", "profiles");
                    if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                    string uniqueFileName = Guid.NewGuid().ToString() + ".jpg";
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    byte[] imageBytes = Convert.FromBase64String(base64Data);
                    await File.WriteAllBytesAsync(filePath, imageBytes);

                    user.ProfilePictureUrl = $"http://db-inventario-api.somee.com/images/profiles/{uniqueFileName}";
                    await _workFlow.CompleteAsync();

                    return (true, user.ProfilePictureUrl, "Foto actualizada correctamente.");
                }
                catch (Exception ex)
                {
                    return (false, "", ex.Message);
                }
            }

            return (false, "", "No se envió ninguna imagen.");
        }

        public async Task<(bool Success, string Secret, string QrUri)> Generate2FAAsync(int id)
        {
            var user = await _workFlow.Repository<User>().GetByIdAsync(id);
            if (user == null) return (false, "", "");

            var key = KeyGeneration.GenerateRandomKey(20);
            var secret = Base32Encoding.ToString(key);
            user.TwoFactorSecret = secret;
            await _workFlow.CompleteAsync();

            var qrUri = $"otpauth://totp/ControlInventario:{user.Username}?secret={secret}&issuer=ControlInventarioCorp";
            return (true, secret, qrUri);
        }

        public async Task<bool> Enable2FAAsync(int id, string code)
        {
            var user = await _workFlow.Repository<User>().GetByIdAsync(id);
            if (user == null || string.IsNullOrEmpty(user.TwoFactorSecret)) return false;

            var totp = new Totp(Base32Encoding.ToBytes(user.TwoFactorSecret));
            if (totp.VerifyTotp(code, out _, window: new VerificationWindow(2, 2)))
            {
                user.IsTwoFactorEnabled = true;
                await _workFlow.CompleteAsync();
                return true;
            }
            return false;
        }

        public async Task<bool> Disable2FAAsync(int id)
        {
            var user = await _workFlow.Repository<User>().GetByIdAsync(id);
            if (user == null) return false;
            user.IsTwoFactorEnabled = false;
            user.TwoFactorSecret = null;
            await _workFlow.CompleteAsync();
            return true;
        }

        public async Task<(bool Success, string Message)> ApproveEmployeeAsync(int id)
        {
            var userMatch = await _workFlow.Repository<User>().GetAllWithIncludeAsync(u => u.Employee!);
            var user = userMatch.FirstOrDefault(u => u.Id == id);

            if (user == null) return (false, "Usuario no encontrado.");

            user.StatusId = 1;
            if (user.Employee != null)
            {
                user.Employee.StatusId = 1;
            }

            await _workFlow.CompleteAsync();

            var perfiles = await _workFlow.Repository<Profile>().GetAllAsync();
            var configuracion = perfiles.FirstOrDefault(p => !string.IsNullOrEmpty(p.SmtpEmail));

            if (configuracion != null && !string.IsNullOrEmpty(user.Email))
            {
                _ = EnviarCorreoActivacionExitosaAsync(user, configuracion.SmtpEmail!, configuracion.SmtpPassword!);
            }

            return (true, "Aprobado con éxito.");
        }

        public async Task<(bool Success, string Message)> TestEmailConnectionAsync(string email, string password)
        {
            try
            {
                var smtpClient = new SmtpClient("smtp.gmail.com")
                {
                    Port = 587,
                    Credentials = new NetworkCredential(email, password),
                    EnableSsl = true,
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(email, "Control Inventario - Test"),
                    Subject = "✅ Prueba de Conexión Exitosa",
                    Body = "<div style='font-family: Arial; padding: 20px; border: 1px solid #ddd; border-radius: 10px; text-align: center;'><h2 style='color: #2ECC71;'>¡Conexión Exitosa!</h2><p>El sistema tiene acceso para enviar alertas.</p></div>",
                    IsBodyHtml = true,
                };

                mailMessage.To.Add(email);
                await smtpClient.SendMailAsync(mailMessage);
                return (true, "Correo enviado con éxito.");
            }
            catch (Exception ex)
            {
                return (false, ex.InnerException?.Message ?? ex.Message);
            }
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
                string htmlBody = $@"<div style='font-family: Arial, sans-serif; max-width: 600px; margin: auto; border: 1px solid #ddd; padding: 20px; border-radius: 10px; background-color: #ffffff; color: #333333;'><h2 style='color: #E74C3C; text-align: center;'>⚠️ Aprobación de Personal Requerida</h2><p>Hola,</p><p>Se ha registrado un nuevo empleado de emergencia en la plataforma de inventarios utilizando tus credenciales de emisor.</p><hr style='border: none; border-top: 1px solid #eee;' /><p><b>Detalles del registro:</b></p><ul style='list-style: none; padding-left: 0;'><li style='margin-bottom: 5px;'><b>Nombre del Colaborador:</b> {nombreCompleto}</li><li style='margin-bottom: 5px;'><b>Nombre de Usuario:</b> {user.Username}</li><li style='margin-bottom: 5px;'><b>Fecha/Hora de Alta:</b> {DateTime.Now:dd/MM/yyyy HH:mm}</li></ul><hr style='border: none; border-top: 1px solid #eee;' /><p>La cuenta se encuentra actualmente en estado <b>Pendiente de Validación (Estatus 2)</b>. Tienes un plazo de <b>48 horas</b> para confirmar su acceso definitivo.</p><br/><div style='text-align: center;'><a href='{linkAprobacion}' style='background-color: #2ECC71; color: white; padding: 14px 30px; text-decoration: none; font-weight: bold; border-radius: 5px; font-size: 16px; display: inline-block;'>Validar y Activar Empleado</a></div></div>";

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(remitente, "Control Inventario - Seguridad"),
                    Subject = "URGENTE: Validación de nuevo empleado requerida",
                    Body = htmlBody,
                    IsBodyHtml = true,
                };

                string destinatarioFinal = !string.IsNullOrWhiteSpace(approverEmail) ? approverEmail.Trim() : "mercadogarciaalexandro10@gmail.com";
                mailMessage.To.Add(destinatarioFinal);

                await smtpClient.SendMailAsync(mailMessage);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[EMAIL ERROR]: {ex.Message}");
            }
        }

        private async Task EnviarCorreoBienvenidaAsync(User user, string clavePlana, string remitente, string passwordApp)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(user.Email)) return;

                var smtpClient = new SmtpClient("smtp.gmail.com")
                {
                    Port = 587,
                    Credentials = new NetworkCredential(remitente, passwordApp),
                    EnableSsl = true,
                };

                string htmlBody = $@"<div style='font-family: Arial, sans-serif; max-width: 600px; margin: auto; border: 1px solid #ddd; padding: 20px; border-radius: 10px; background-color: #f9f9f9;'><h2 style='color: #2980b9; text-align: center;'>¡Bienvenido al Equipo!</h2><p>Hola <b>{user.Employee?.FirstName}</b>,</p><p>Tu cuenta corporativa ha sido creada. Credenciales:</p><p><b>Usuario:</b> {user.Username}</p><p><b>Contraseña:</b> {clavePlana}</p></div>";

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(remitente, "Control Inventario - RRHH"),
                    Subject = "Bienvenido - Tus credenciales de acceso",
                    Body = htmlBody,
                    IsBodyHtml = true,
                };

                mailMessage.To.Add(user.Email.Trim());
                await smtpClient.SendMailAsync(mailMessage);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[EMAIL ERROR]: {ex.Message}");
            }
        }

        private async Task EnviarCorreoActivacionExitosaAsync(User user, string remitente, string passwordApp)
        {
            try
            {
                var smtpClient = new SmtpClient("smtp.gmail.com")
                {
                    Port = 587,
                    Credentials = new NetworkCredential(remitente, passwordApp),
                    EnableSsl = true,
                };

                string htmlBody = $@"<div style='font-family: Arial, sans-serif; max-width: 600px; margin: auto; border: 1px solid #ddd; padding: 20px; border-radius: 10px; text-align: center;'><h2 style='color: #2ECC71;'>¡Tu cuenta ha sido activada! 🎉</h2><p>Hola <b>{user.Employee?.FirstName}</b>, Gerencia ha aprobado tu perfil.</p></div>";

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(remitente, "Control Inventario - RRHH"),
                    Subject = "¡Cuenta Activada! Ya puedes ingresar",
                    Body = htmlBody,
                    IsBodyHtml = true,
                };

                mailMessage.To.Add(user.Email!.Trim());
                await smtpClient.SendMailAsync(mailMessage);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[EMAIL ERROR]: {ex.Message}");
            }
        }
    }
}