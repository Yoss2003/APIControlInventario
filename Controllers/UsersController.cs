using InventoryAPI.Models;
using InventoryAPI.Models.DTO;
using InventoryAPI.Data;
using InventoryAPI.Security;
using Microsoft.EntityFrameworkCore;
using InventoryAPI.Services.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryAPI.Controllers
{
    public class UsersController(IUserService userService, IWebHostEnvironment env, IFcmNotificationService fcmNotificationService, ICompanyLicenseService licenseService, InventoryAccessPolicy access, AppDbContext db) : BaseApiController
    {
        private readonly IUserService _userService = userService;
        private readonly IWebHostEnvironment _env = env;
        private readonly IFcmNotificationService _fcmNotificationService = fcmNotificationService;
        private readonly ICompanyLicenseService _licenseService = licenseService;

        [HttpGet]
        public async Task<ActionResult<IEnumerable<UserDTO>>> GetUsers()
        {
            if (!await access.HasAsync(User, InventoryPermissionCodes.ManageUsers)) return Forbid();
            int companyId = ObtenerEmpresaSegura();
            var users = await _userService.GetUsersDtoAsync();
            return Ok(users.Where(u => u.CompanyId == companyId).ToList());
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<UserDTO>> GetUser(int id)
        {
            if (id != ObtenerUsuarioSeguro() && !await access.HasAsync(User, InventoryPermissionCodes.ManageUsers)) return Forbid();
            int companyId = ObtenerEmpresaSegura();
            var userDto = await _userService.GetUserDtoByIdAsync(id);

            if (userDto == null || userDto.CompanyId != companyId)
                return NotFound(new { mensaje = "El usuario no fue localizado o no tienes permisos." });

            return Ok(userDto);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutUser(int id, [FromBody] User userActualizado)
        {
            if (!await access.HasAsync(User, InventoryPermissionCodes.ManageUsers)) return Forbid();
            int companyId = ObtenerEmpresaSegura();
            userActualizado.CompanyId = companyId;

            var existingUser = await _userService.GetUserDtoByIdAsync(id);
            if (existingUser == null || existingUser.CompanyId != companyId)
                return NotFound(new { mensaje = "El usuario no existe o no pertenece a tu sucursal." });

            if (existingUser.RoleId == 1 || (existingUser.RoleId == 2 && !InventoryAccessPolicy.IsDeveloper(User))) return Forbid();
            if (userActualizado.RoleId is 1 or 2) return Forbid();
            if (userActualizado.RoleId > 0 &&
                !await access.CanAssignRoleAsync(User, userActualizado.RoleId))
                return Forbid();
            if (id == ObtenerUsuarioSeguro() && userActualizado.RoleId > 0 &&
                userActualizado.RoleId != existingUser.RoleId)
                return Conflict(new { error = "No puedes cambiar tu propio rol." });
            userActualizado.Password = null; // Las contraseñas se cambian por un proceso separado.
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
            if (!await access.HasAsync(User, InventoryPermissionCodes.ManageUsers)) return Forbid();
            if (user.RoleId is 1 or 2) return Conflict(new { error = "Usa Gestión Dev para registrar el administrador inicial. El rol desarrollador está protegido." });
            if (!await access.CanAssignRoleAsync(User, user.RoleId))
                return Forbid();
            if (string.IsNullOrWhiteSpace(user.Password) || user.Password.Length < 12)
                return BadRequest(new { error = "Se requiere una contraseña de al menos 12 caracteres." });
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
            var created = (User)result.Data;
            return CreatedAtAction(nameof(GetUser), new { id = created.Id },
                new { created.Id, created.Username, created.Email, created.RoleId, created.CompanyId,
                    personalInventoryProvisioned = true });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            if (!await access.HasAsync(User, InventoryPermissionCodes.ManageUsers)) return Forbid();
            if (id == ObtenerUsuarioSeguro()) return Conflict(new { error = "No puedes desactivar tu propia cuenta." });
            int companyId = ObtenerEmpresaSegura();
            string deletedBy = ObtenerUsuarioSeguro().ToString();

            var existingUser = await _userService.GetUserDtoByIdAsync(id);
            if (existingUser == null || existingUser.CompanyId != companyId) return NotFound();
            if (existingUser.RoleId == 1 || (existingUser.RoleId == 2 && !InventoryAccessPolicy.IsDeveloper(User))) return Forbid();
            if (existingUser.RoleId == 2 &&
                await db.Users.CountAsync(u => u.CompanyId == companyId && u.RoleId == 2 && u.IsActive) <= 1)
                return Conflict(new { error = "No se puede desactivar el último administrador." });
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
            if (!await CanManageTargetAsync(id)) return Forbid();
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
            if (id != ObtenerUsuarioSeguro()) return Forbid();
            var (Success, Secret, QrUri) = await _userService.Generate2FAAsync(id);
            if (!Success) return NotFound();
            return Ok(new { secret = Secret, qrUri = QrUri });
        }

        [HttpPost("{id}/enable-2fa")]
        public async Task<IActionResult> Enable2FA(int id, [FromBody] string code)
        {
            if (id != ObtenerUsuarioSeguro()) return Forbid();
            bool success = await _userService.Enable2FAAsync(id, code);
            if (success) return Ok(new { mensaje = "Activado" });
            return BadRequest(new { mensaje = "Inválido" });
        }

        [HttpPost("{id}/disable-2fa")]
        public async Task<IActionResult> Disable2FA(int id)
        {
            if (id != ObtenerUsuarioSeguro()) return Forbid();
            bool success = await _userService.Disable2FAAsync(id);
            if (!success) return NotFound();
            return Ok(new { mensaje = "Desactivado" });
        }

        [HttpPost("TestEmailConfiguration")]
        public async Task<IActionResult> TestEmailConfiguration([FromBody] SmtpTestRequest request)
        {
            if (!InventoryAccessPolicy.IsDeveloper(User)) return Forbid();
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                return BadRequest(new { mensaje = "El correo y la contraseña son obligatorios." });

            var (Success, Message) = await _userService.TestEmailConnectionAsync(request.Email, request.Password);
            if (!Success) return StatusCode(500, new { error = "Fallo de conexión", detalle = Message });

            return Ok(new { mensaje = Message });
        }

        // Los antiguos enlaces GET de aprobación por ID eran falsificables.
        // No permitir una transición de estado por un enlace público.
        [HttpGet("ApproveAccount/{id:int}")]
        [AllowAnonymous]
        public IActionResult LegacyApproveDisabled(int id) => StatusCode(410,
            new { error = "La aprobación por enlace público ha sido retirada. Usa Gestión de usuarios." });

        [HttpPost("ApproveAccount/{id:int}")]
        public async Task<IActionResult> ApproveEmployee(int id)
        {
            if (!await access.HasAsync(User, InventoryPermissionCodes.ManageUsers)) return Forbid();
            var target = await db.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == id && u.CompanyId == ObtenerEmpresaSegura());
            if (target is null) return NotFound();
            if (target.RoleId == 1 ||
                (target.RoleId == 2 && !InventoryAccessPolicy.IsDeveloper(User))) return Forbid();
            var (success, message) = await _userService.ApproveEmployeeAsync(id);
            return success ? Ok(new { message }) : Conflict(new { error = message });
        }

        [HttpPut("{id}/fcm-token")]
        public async Task<IActionResult> UpdateFcmToken(int id, [FromBody] string fcmToken)
        {
            if (id != ObtenerUsuarioSeguro()) return Forbid();
            if (string.IsNullOrWhiteSpace(fcmToken))
                return BadRequest(new { mensaje = "El token no puede estar vacío." });

            bool result = await _userService.UpdateFcmTokenAsync(id, fcmToken);

            if (result)
                return Ok(new { mensaje = "Token de notificaciones actualizado correctamente." });

            return NotFound(new { mensaje = "Usuario no encontrado." });
        }

        [HttpPost("test-push")]
        public async Task<IActionResult> TestPushNotification([FromBody] string fcmToken)
        {
            if (!InventoryAccessPolicy.IsDeveloper(User)) return Forbid();
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
            if (!await access.HasAsync(User, InventoryPermissionCodes.ManageUsers) ||
                !await CanManageTargetAsync(id)) return Forbid();
            var (Success, Message) = await _userService.ResendWelcomeEmailAsync(id);

            if (!Success) return BadRequest(new { mensaje = Message });

            return Ok(new { mensaje = Message });
        }

        private async Task<bool> CanManageTargetAsync(int id)
        {
            if (!await db.Users.AnyAsync(u => u.Id == id && u.CompanyId == ObtenerEmpresaSegura()))
                return false;
            return id == ObtenerUsuarioSeguro() ||
                await access.HasAsync(User, InventoryPermissionCodes.ManageUsers);
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