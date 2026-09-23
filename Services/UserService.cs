using ControlInventario.Shared.Models;
using ControlInventario.Shared.Models.DTO;
using InventoryAPI.Repositories;
using InventoryAPI.Repositories.IRepositories;
using InventoryAPI.Services.IServices;
using OtpNet;
using System.Diagnostics;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;

namespace InventoryAPI.Services
{
    public class UserService(IWorkFlow workFlow) : WorkContainer<User>(workFlow), IUserService
    {
        public async Task<IEnumerable<UserDTO>> GetUsersDtoAsync()
        {
            var users = await _workFlow.Repository<User>().GetAllWithIncludeAsync(u => u.Role!, u => u.Employee!);
            return [.. users.Select(u => MapToDto(u))];
        }

        public async Task<UserDTO?> GetUserDtoByIdAsync(int id)
        {
            var userMatch = await _workFlow.Repository<User>().FindAsync(u => u.Id == id);
            var user = userMatch.FirstOrDefault();
            if (user == null) return null;

            var fullUserMatch = await _workFlow.Repository<User>().GetAllWithIncludeAsync(u => u.Role!, u => u.Employee!);
            var fullUser = fullUserMatch.FirstOrDefault(u => u.Id == id) ?? user;

            return MapToDto(fullUser);
        }

        private static UserDTO MapToDto(User u)
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
                userDb.Employee ??= new Employee();
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
            var usuariosExistentes = await _workFlow.Repository<User>().FindAsync(u => u.Username == user.Username);
            if (usuariosExistentes.Any())
            {
                return (false, new { mensaje = "Este usuario fue un trabajador, por lo que no puedes agregarlo." }, "Duplicado");
            }

            var perfiles = await _workFlow.Repository<Profile>().GetAllAsync();
            var perfilConfigurado = perfiles.FirstOrDefault(p => !string.IsNullOrEmpty(p.SmtpEmail) && !string.IsNullOrEmpty(p.SmtpPassword));
            if (perfilConfigurado == null)
            {
                return (false, new { requiresSmtpConfiguration = true, mensaje = "El sistema requiere que configures el correo emisario (SMTP) en los Ajustes antes de registrar personal." }, "SMTP no configurado.");
            }

            user.Employee ??= new Employee();
            user.Employee.JobPositionId = (user.Employee.JobPositionId == null || user.Employee.JobPositionId <= 0) ? 1 : user.Employee.JobPositionId;
            user.Employee.AreaId = (user.Employee.AreaId == null || user.Employee.AreaId <= 0) ? 1 : user.Employee.AreaId;
            user.Employee.ContractTypeId = (user.Employee.ContractTypeId == null || user.Employee.ContractTypeId <= 0) ? 1 : user.Employee.ContractTypeId;
            user.Employee.Age = user.Employee.Age ?? 0;
            user.IsActive = true;
            user.StatusId = 2;
            user.Employee.StatusId = 2;
            user.Role = null;

            // 1. GUARDAMOS LA CLAVE PLANA PARA EL CORREO
            string clavePlana = user.Password!;

            // 2. ENCRIPTAMOS LA CLAVE PARA LA BASE DE DATOS (SHA256)
            user.Password = GenerarHashSHA256(clavePlana);

            if (!string.IsNullOrEmpty(user.ProfilePictureUrl) && user.ProfilePictureUrl.Length > 500)
            {
                try
                {
                    string base64Data = user.ProfilePictureUrl;
                    if (base64Data.Contains(','))
                    {
                        base64Data = base64Data[(base64Data.IndexOf(',') + 1)..];
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
                // 3. INICIAMOS LA TRANSACCIÓN
                await _workFlow.BeginTransactionAsync();

                // Guardamos el usuario (con la contraseña ya encriptada)
                await _workFlow.Repository<User>().AddAsync(user);
                await _workFlow.CompleteAsync();

                bool esAltoNivel = user.RoleId == 1 || user.RoleId == 2;

                if (esAltoNivel)
                {
                    string fechaActual = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
                    string prefijoFecha = DateTime.Now.ToString("ddMM");

                    var nuevoInventario = new Inventory
                    {
                        InventoryName = $"{user.Username}_Invent_{prefijoFecha}",
                        CreationDate = fechaActual,
                        ModificationDate = fechaActual,
                        UserId = user.Id,
                        Username = user.Username!,
                        Alias = "Inventario Principal",
                        CompanyId = user.CompanyId ?? 1
                    };

                    await _workFlow.Repository<Inventory>().AddAsync(nuevoInventario);
                    await _workFlow.CompleteAsync();
                }
                else if (user.AssignedInventoryId.HasValue && user.AssignedInventoryId.Value > 0)
                {
                    var nuevoAcceso = new SharedInventory
                    {
                        InventoryId = user.AssignedInventoryId.Value,
                        UserId = user.Id,
                        AccessLevel = user.RoleId == 5 ? SharedInventory.AccessMode.Lector : SharedInventory.AccessMode.Editor,
                        SharedDate = DateTime.Now,
                        CompanyId = user.CompanyId ?? 1
                    };

                    await _workFlow.Repository<SharedInventory>().AddAsync(nuevoAcceso);
                    await _workFlow.CompleteAsync();
                }

                // Confirmamos la transacción
                await _workFlow.CommitTransactionAsync();

                // Enviamos los correos (usando la clave plana)
                string nombreFiltro = $"{user.Employee.FirstName} {user.Employee.LastName}".Trim();
                _ = EnviarCorreoAprobacionAsync(user, nombreFiltro, perfilConfigurado.SmtpEmail!, perfilConfigurado.SmtpPassword!, perfilConfigurado.SmtpApproverEmail!);
                _ = EnviarCorreoBienvenidaAsync(user, clavePlana, perfilConfigurado.SmtpEmail!, perfilConfigurado.SmtpPassword!);

                return (true, user, "Usuario creado exitosamente.");
            }
            catch (Exception ex)
            {
                await _workFlow.RollbackTransactionAsync();
                return (false, ex.InnerException?.Message ?? ex.Message, "Error crítico SQL.");
            }
        }

        // MÉTODO PARA ENCRIPTAR CONTRASEÑA
        private static string GenerarHashSHA256(string textoPlano)
        {
            byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(textoPlano));
            StringBuilder builder = new();
            for (int i = 0; i < bytes.Length; i++)
            {
                builder.Append(bytes[i].ToString("X2"));
            }
            return builder.ToString();
        }

        public async Task<(bool Success, User? User, string Message, bool Requires2FA, bool RequirePasswordChange, bool AccountPending)> LoginAsync(LoginRequestDTO request)
        {
            var usersMatch = await _workFlow.Repository<User>().GetAllWithIncludeAsync(
                u => u.Role!,
                u => u.Employee!,
                u => u.Company!
            );

            string claveHasheada = GenerarHashSHA256(request.Password);

            var targetUser = usersMatch.FirstOrDefault(u => u.Username == request.Username && string.Equals(u.Password, claveHasheada, StringComparison.OrdinalIgnoreCase));

            if (targetUser == null) return (false, null, "Usuario o contraseña incorrectos", false, false, false);

            int DEVELOPER_ROLE_ID = 1;
            if (targetUser.RoleId != DEVELOPER_ROLE_ID && targetUser.CompanyId != request.CompanyId)
            {
                return (false, null, "Estas credenciales no están autorizadas para esta empresa o sucursal.", false, false, false);
            }

            if (!targetUser.IsActive || targetUser.StatusId != 1)
                return (false, null, "Tu cuenta se encuentra inactiva o pendiente de validación.", false, false, true);

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
                    if (base64Data.Contains(',', StringComparison.OrdinalIgnoreCase))
                        base64Data = base64Data[(base64Data.IndexOf(',', StringComparison.OrdinalIgnoreCase) + 1)..];
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

            if (user == null) return (false, "Usuario no encontrado en el sistema.");

            if (user.StatusId == 1) return (false, "Este colaborador ya había sido aprobado y activado previamente.");

            user.StatusId = 1;
            user.Employee?.StatusId = 1;

            await _workFlow.CompleteAsync();

            var perfiles = await _workFlow.Repository<Profile>().GetAllAsync();
            var configuracion = perfiles.FirstOrDefault(p => !string.IsNullOrEmpty(p.SmtpEmail));

            if (configuracion != null && !string.IsNullOrEmpty(user.Email))
            {
                _ = EnviarCorreoActivacionExitosaAsync(user, configuracion.SmtpEmail!, configuracion.SmtpPassword!);
            }

            return (true, "El colaborador ha sido activado exitosamente y ya puede ingresar al sistema.");
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

        private static async Task EnviarCorreoAprobacionAsync(User user, string nombreCompleto, string remitente, string passwordApp, string approverEmail)
        {
            try
            {
                var smtpClient = new SmtpClient("smtp.gmail.com")
                {
                    Port = 587,
                    Credentials = new NetworkCredential(remitente, passwordApp),
                    EnableSsl = true,
                };

                string linkAprobacion = $"http://db-inventario-api.somee.com/api/Users/ApproveAccount/{user.Id}"; // Ajustado para el nuevo endpoint

                string htmlBody = $@"
                <div style='font-family: ""Segoe UI"", Arial, sans-serif; max-width: 600px; margin: auto; border: 1px solid #e0e0e0; padding: 30px; border-radius: 12px; background-color: #ffffff; color: #333333; box-shadow: 0 4px 6px rgba(0,0,0,0.05);'>
                    <div style='text-align: center; margin-bottom: 20px;'>
                        <h2 style='color: #E74C3C; margin: 0;'>⚠️ Aprobación de Cuenta Requerida</h2>
                    </div>
                    <p style='font-size: 16px; line-height: 1.5;'>Hola Administrador,</p>
                    <p style='font-size: 15px; line-height: 1.5; color: #555;'>Se ha registrado un nuevo colaborador en la plataforma y requiere tu autorización para activar su acceso.</p>
            
                    <div style='background-color: #f8f9fa; padding: 15px; border-left: 4px solid #E74C3C; margin: 20px 0;'>
                        <h3 style='margin-top: 0; color: #2c3e50; font-size: 16px;'>Detalles del Registro:</h3>
                        <ul style='list-style: none; padding-left: 0; margin: 0; font-size: 14px;'>
                            <li style='margin-bottom: 8px;'><strong>Colaborador:</strong> {nombreCompleto}</li>
                            <li style='margin-bottom: 8px;'><strong>Usuario:</strong> {user.Username}</li>
                            <li style='margin-bottom: 8px;'><strong>Fecha de Alta:</strong> {DateTime.Now:dd/MM/yyyy HH:mm}</li>
                        </ul>
                    </div>
            
                    <p style='font-size: 14px; color: #666; margin-bottom: 25px;'><strong>Atención:</strong> La cuenta está actualmente en estado <em>Pendiente de Validación</em>. El usuario no podrá iniciar sesión hasta que apruebes esta solicitud.</p>
            
                    <div style='text-align: center;'>
                        <a href='{linkAprobacion}' style='background-color: #2ECC71; color: white; padding: 14px 35px; text-decoration: none; font-weight: bold; border-radius: 6px; font-size: 16px; display: inline-block; box-shadow: 0 2px 4px rgba(46, 204, 113, 0.3);'>Aprobar y Activar Cuenta</a>
                    </div>
            
                    <hr style='border: none; border-top: 1px solid #eee; margin: 30px 0;' />
                    <p style='font-size: 12px; color: #999; text-align: center;'>Este es un mensaje automático del Sistema de Control de Inventario. Por favor no responder a este correo.</p>
                </div>";

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(remitente, "Control Inventario - Seguridad"),
                    Subject = "URGENTE: Validación de nuevo empleado requerida",
                    Body = htmlBody,
                    IsBodyHtml = true,
                };

                string destinatarioFinal = !string.IsNullOrWhiteSpace(approverEmail) ? approverEmail.Trim() : remitente;
                mailMessage.To.Add(destinatarioFinal);

                await smtpClient.SendMailAsync(mailMessage);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[EMAIL ERROR]: {ex.Message}");
            }
        }

        private static async Task EnviarCorreoBienvenidaAsync(User user, string clavePlana, string remitente, string passwordApp)
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

                string htmlBody = $@"
                <div style='font-family: ""Segoe UI"", Arial, sans-serif; max-width: 600px; margin: auto; border: 1px solid #e0e0e0; padding: 30px; border-radius: 12px; background-color: #f9fbfd; color: #333333; box-shadow: 0 4px 6px rgba(0,0,0,0.05);'>
                    <div style='text-align: center; margin-bottom: 25px;'>
                        <h2 style='color: #2980b9; margin: 0; font-size: 24px;'>¡Bienvenido al Equipo, {user.Employee?.FirstName}!</h2>
                    </div>
            
                    <p style='font-size: 16px; line-height: 1.6;'>Tu cuenta corporativa ha sido creada exitosamente. A continuación, te proporcionamos tus credenciales de acceso temporal:</p>
            
                    <div style='background-color: #ffffff; padding: 20px; border-radius: 8px; border: 1px solid #e1e8ed; margin: 25px 0; text-align: center;'>
                        <p style='margin: 5px 0; font-size: 16px;'><strong>Usuario:</strong> <span style='color: #2980b9; font-size: 18px;'>{user.Username}</span></p>
                        <p style='margin: 5px 0; font-size: 16px;'><strong>Contraseña temporal:</strong> <span style='font-family: monospace; background-color: #f1f2f6; padding: 4px 8px; border-radius: 4px; font-size: 16px; letter-spacing: 1px;'>{clavePlana}</span></p>
                    </div>
            
                    <div style='background-color: #fff3cd; color: #856404; padding: 15px; border-left: 4px solid #ffeeba; border-radius: 4px; margin-bottom: 20px;'>
                        <h4 style='margin-top: 0; margin-bottom: 10px; font-size: 15px;'>🔒 Recomendaciones de Seguridad:</h4>
                        <ul style='margin: 0; padding-left: 20px; font-size: 14px; line-height: 1.5;'>
                            <li>Por tu seguridad, <strong>el sistema te pedirá cambiar esta contraseña</strong> obligatoriamente la primera vez que inicies sesión.</li>
                            <li>No compartas tus credenciales con nadie.</li>
                            <li>Si no reconoces este registro, contacta a tu administrador inmediatamente.</li>
                        </ul>
                    </div>
            
                    <p style='font-size: 14px; color: #555;'><em>Nota: Tu cuenta está siendo validada por la administración. Podrás ingresar al sistema una vez que sea aprobada.</em></p>
            
                    <hr style='border: none; border-top: 1px solid #eee; margin: 30px 0;' />
                    <p style='font-size: 12px; color: #999; text-align: center;'>Este es un mensaje automático. Por favor no responder a este correo.</p>
                </div>";

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(remitente, "Control Inventario - Recursos Humanos"),
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

        private static async Task EnviarCorreoActivacionExitosaAsync(User user, string remitente, string passwordApp)
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