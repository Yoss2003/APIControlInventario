using InventoryAPI.Models;
using InventoryAPI.Models.DTO;
using InventoryAPI.Services.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryAPI.Controllers
{
    public class UsersController(IUserService userService, IWebHostEnvironment env, IFcmNotificationService fcmNotificationService, ICompanyLicenseService licenseService) : BaseApiController
    {
        private readonly IUserService _userService = userService;
        private readonly IWebHostEnvironment _env = env;
        private readonly IFcmNotificationService _fcmNotificationService = fcmNotificationService;
        private readonly ICompanyLicenseService _licenseService = licenseService;

        [HttpGet]
        public async Task<ActionResult<IEnumerable<UserDTO>>> GetUsers()
        {
            int companyId = ObtenerEmpresaSegura();
            var users = await _userService.GetUsersDtoAsync();
            return Ok(users.Where(u => u.CompanyId == companyId).ToList());
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<UserDTO>> GetUser(int id)
        {
            int companyId = ObtenerEmpresaSegura();
            var userDto = await _userService.GetUserDtoByIdAsync(id);

            if (userDto == null || userDto.CompanyId != companyId)
                return NotFound(new { mensaje = "El usuario no fue localizado o no tienes permisos." });

            return Ok(userDto);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutUser(int id, [FromBody] User userActualizado)
        {
            int companyId = ObtenerEmpresaSegura();
            userActualizado.CompanyId = companyId;

            var existingUser = await _userService.GetUserDtoByIdAsync(id);
            if (existingUser == null || existingUser.CompanyId != companyId)
                return NotFound(new { mensaje = "El usuario no existe o no pertenece a tu sucursal." });

            var (Success, Message) = await _userService.UpdateUserAsync(id, userActualizado);
            if (!Success)
            {
                if (Message.Contains("no existe")) return NotFound(new { mensaje = Message });
                if (Message.Contains("no coincide")) return BadRequest(new { mensaje = Message });
                return StatusCode(500, new { error = "Error al editar", detalle = Message });
            }
            return NoContent();
        }

        [HttpPost]
        public async Task<ActionResult<User>> PostUser([FromBody] User user)
        {
            user.CompanyId = ObtenerEmpresaSegura();

            int companyId = user.CompanyId.Value;

            (bool allowed, string? licenseError) =
                await _licenseService.CanCreateUserAsync(companyId);

            if (!allowed)
            {
                return Conflict(new { error = licenseError });
            }

            var result = await _userService.CreateUserAsync(user, _env.ContentRootPath);

            if (!result.Success) return BadRequest(result.Data);
            return CreatedAtAction(nameof(GetUser), new { id = ((User)result.Data).Id }, result.Data);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            int companyId = ObtenerEmpresaSegura();
            string deletedBy = ObtenerUsuarioSeguro().ToString();

            var existingUser = await _userService.GetUserDtoByIdAsync(id);
            if (existingUser == null || existingUser.CompanyId != companyId) return NotFound();

            var success = await _userService.DeleteAsync(id, deletedBy);
            if (!success) return NotFound();

            return NoContent();
        }

        [HttpPost("Login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginRequestDTO request)
        {
            var result = await _userService.LoginAsync(request);
            if (!result.Success)
            {
                if (result.Requires2FA) return Unauthorized(new { requires2FA = true, mensaje = result.Message });
                if (result.AccountPending) return Unauthorized(new { accountPending = true, mensaje = result.Message });
                return Unauthorized(new { mensaje = result.Message });
            }
            if (result.RequirePasswordChange) return Ok(new { requirePasswordChange = true });
            var u = result.User!;
            return Ok(new { u.Id, u.Username, u.Email, u.RoleId, companyId = request.CompanyId, u.Token });
        }

        // Compatibilidad con clientes antiguos: esta ruta no realiza cambios de contraseña.
        // El flujo seguro se encuentra en api/password-recovery.
        [HttpPost("ChangeInitialPassword")]
        [AllowAnonymous]
        public IActionResult ChangeInitialPassword()
        {
            return StatusCode(410, new { error = "Utiliza la recuperación aprobada en Gestión Dev." });
        }

        [HttpPut("{id}/UpdatePhoto")]
        public async Task<IActionResult> UpdatePhoto(int id, [FromBody] PhotoUpdateDTO request)
        {
            var result = await _userService.UpdatePhotoAsync(id, request.Base64Image, _env.ContentRootPath);
            if (!result.Success)
            {
                if (result.Message.Contains("no encontrado")) return NotFound(new { mensaje = result.Message });
                if (result.Message.Contains("ninguna imagen")) return BadRequest(new { mensaje = result.Message });
                return StatusCode(500, new { mensaje = "Fallo al guardar imagen en Somee.", detalle = result.Message });
            }
            return Ok(new { result.Url, mensaje = result.Message });
        }

        [HttpPost("{id}/generate-2fa")]
        public async Task<IActionResult> Generate2FA(int id)
        {
            var (Success, Secret, QrUri) = await _userService.Generate2FAAsync(id);
            if (!Success) return NotFound();
            return Ok(new { secret = Secret, qrUri = QrUri });
        }

        [HttpPost("{id}/enable-2fa")]
        public async Task<IActionResult> Enable2FA(int id, [FromBody] string code)
        {
            bool success = await _userService.Enable2FAAsync(id, code);
            if (success) return Ok(new { mensaje = "Activado" });
            return BadRequest(new { mensaje = "Inválido" });
        }

        [HttpPost("{id}/disable-2fa")]
        public async Task<IActionResult> Disable2FA(int id)
        {
            bool success = await _userService.Disable2FAAsync(id);
            if (!success) return NotFound();
            return Ok(new { mensaje = "Desactivado" });
        }

        [HttpPost("TestEmailConfiguration")]
        [AllowAnonymous]
        public async Task<IActionResult> TestEmailConfiguration([FromBody] SmtpTestRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                return BadRequest(new { mensaje = "El correo y la contraseña son obligatorios." });

            var (Success, Message) = await _userService.TestEmailConnectionAsync(request.Email, request.Password);
            if (!Success) return StatusCode(500, new { error = "Fallo de conexión", detalle = Message });

            return Ok(new { mensaje = Message });
        }

        [HttpGet("ApproveAccount/{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> ApproveAccountFromEmail(int id)
        {
            var (Success, Message) = await _userService.ApproveEmployeeAsync(id);

            string colorBase, colorFondo, icono, titulo, mensaje;
            if (Success)
            {
                colorBase = "#10B981"; colorFondo = "#ECFDF5";
                icono = @"<svg class='icon' fill='none' stroke='currentColor' viewBox='0 0 24 24'><path stroke-linecap='round' stroke-linejoin='round' stroke-width='2' d='M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z'></path></svg>";
                titulo = "Cuenta Aprobada"; mensaje = Message;
            }
            else if (Message.Contains("ya había sido aprobado"))
            {
                colorBase = "#F59E0B"; colorFondo = "#FFFBEB";
                icono = @"<svg class='icon' fill='none' stroke='currentColor' viewBox='0 0 24 24'><path stroke-linecap='round' stroke-linejoin='round' stroke-width='2' d='M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z'></path></svg>";
                titulo = "Acción Completada"; mensaje = Message;
            }
            else
            {
                colorBase = "#EF4444"; colorFondo = "#FEF2F2";
                icono = @"<svg class='icon' fill='none' stroke='currentColor' viewBox='0 0 24 24'><path stroke-linecap='round' stroke-linejoin='round' stroke-width='2' d='M10 14l2-2m0 0l2-2m-2 2l-2-2m2 2l2 2m7-2a9 9 0 11-18 0 9 9 0 0118 0z'></path></svg>";
                titulo = "Acceso Denegado"; mensaje = Message;
            }

            string htmlResponse = $@"
            <!DOCTYPE html>
            <html lang='es'>
            <head>
                <meta charset='UTF-8'>
                <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                <title>{titulo} - Control Inventario</title>
                <style>
                    body {{ margin: 0; padding: 0; font-family: 'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background-color: #F3F4F6; display: flex; flex-direction: column; align-items: center; justify-content: center; min-height: 100vh; }}
                    .card {{ background-color: #FFFFFF; border-radius: 16px; box-shadow: 0 20px 25px -5px rgba(0, 0, 0, 0.1), 0 10px 10px -5px rgba(0, 0, 0, 0.04); max-width: 420px; width: 90%; padding: 40px 30px; text-align: center; border-top: 6px solid {colorBase}; }}
                    .icon-container {{ background-color: {colorFondo}; color: {colorBase}; width: 80px; height: 80px; border-radius: 50%; display: flex; align-items: center; justify-content: center; margin: 0 auto 24px auto; }}
                    .icon {{ width: 40px; height: 40px; }}
                    h1 {{ color: #111827; font-size: 24px; font-weight: 700; margin: 0 0 16px 0; }}
                    p.message {{ color: #4B5563; font-size: 16px; line-height: 1.5; margin: 0 0 32px 0; }}
                    p.footer {{ color: #9CA3AF; font-size: 13px; margin: 0; border-top: 1px solid #E5E7EB; padding-top: 20px; }}
                </style>
            </head>
            <body>
                <div class='card'>
                    <div class='icon-container'>{icono}</div>
                    <h1>{titulo}</h1>
                    <p class='message'>{mensaje}</p>
                    <p class='footer'>Esta pestaña puede ser cerrada de forma segura.</p>
                </div>
            </body>
            </html>";

            Response.ContentType = "text/html; charset=utf-8";
            await Response.WriteAsync(htmlResponse);
            return new EmptyResult();
        }

        [HttpPut("{id}/fcm-token")]
        [AllowAnonymous]
        public async Task<IActionResult> UpdateFcmToken(int id, [FromBody] string fcmToken)
        {
            if (string.IsNullOrWhiteSpace(fcmToken))
                return BadRequest(new { mensaje = "El token no puede estar vacío." });

            bool result = await _userService.UpdateFcmTokenAsync(id, fcmToken);

            if (result)
                return Ok(new { mensaje = "Token de notificaciones actualizado correctamente." });

            return NotFound(new { mensaje = "Usuario no encontrado." });
        }

        [AllowAnonymous]
        [HttpPost("test-push")]
        public async Task<IActionResult> TestPushNotification([FromBody] string fcmToken)
        {
            var path = Path.Combine(_env.ContentRootPath, "firebase-key.json");
            if (!System.IO.File.Exists(path))
                return StatusCode(500, new { mensaje = $"🔥 ERROR 1: Somee NO encuentra el archivo clave. Ruta buscada: {path}" });

            if (FirebaseAdmin.FirebaseApp.DefaultInstance == null)
                return StatusCode(500, new { mensaje = "🔥 ERROR 2: El archivo existe, pero Google rechazó las credenciales al intentar inicializar." });

            try
            {
                bool result = await _fcmNotificationService.SendPushNotificationAsync(
                    fcmToken,
                    "¡Prueba de Fuego! 🔥",
                    "Si estás leyendo esto, Somee ya puede despertar a tu celular.");

                if (result)
                    return Ok(new { mensaje = "¡Notificación enviada con éxito a Firebase!" });

                return StatusCode(500, new { mensaje = "🔥 ERROR 3: Firebase arrancó, pero falló el envío (revisa FcmNotificationService)." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { mensaje = $"🔥 ERROR 4 (Excepción Crítica): {ex.Message}" });
            }
        }

        [HttpPost("{id}/resend-welcome")]
        public async Task<IActionResult> ResendWelcomeEmail(int id)
        {
            var (Success, Message) = await _userService.ResendWelcomeEmailAsync(id);

            if (!Success) return BadRequest(new { mensaje = Message });

            return Ok(new { mensaje = Message });
        }

        public class SmtpTestRequest
        {
            public string Email { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
        }

        public class PhotoUpdateDTO
        {
            public string Base64Image { get; set; } = string.Empty;
        }
    }
}