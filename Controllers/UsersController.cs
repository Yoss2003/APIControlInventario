using Microsoft.AspNetCore.Mvc;
using InventoryAPI.Services.IServices;
using ControlInventario.Shared.Models;
using ControlInventario.Shared.Models.DTO;

namespace InventoryAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IWebHostEnvironment _env;

        public UsersController(IUserService userService, IWebHostEnvironment env)
        {
            _userService = userService;
            _env = env;
        }

        // GET: api/Users
        [HttpGet]
        public async Task<ActionResult<IEnumerable<UserDTO>>> GetUsers()
        {
            if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader))
                return BadRequest("Falta indicar la sucursal.");
            int companyId = int.Parse(companyIdHeader!);

            // 2. Filtramos los usuarios para que solo devuelva los de la sucursal que hace la petición
            var users = await _userService.GetUsersDtoAsync();
            var companyUsers = users.Where(u => u.CompanyId == companyId).ToList();

            return Ok(companyUsers);
        }

        // GET: api/Users/5
        [HttpGet("{id}")]
        public async Task<ActionResult<UserDTO>> GetUser(int id)
        {
            if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader))
                return BadRequest("Falta indicar la sucursal.");
            int companyId = int.Parse(companyIdHeader!);

            var userDto = await _userService.GetUserDtoByIdAsync(id);

            if (userDto == null || userDto.CompanyId != companyId)
                return NotFound(new { mensaje = "El usuario no fue localizado o no tienes permisos." });

            return Ok(userDto);
        }

        // PUT: api/Users/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutUser(int id, [FromBody] User userActualizado)
        {
            if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader))
                return BadRequest("Falta indicar la sucursal.");
            int companyId = int.Parse(companyIdHeader!);

            userActualizado.CompanyId = companyId;

            var existingUser = await _userService.GetUserDtoByIdAsync(id);
            if (existingUser == null || existingUser.CompanyId != companyId)
                return NotFound(new { mensaje = "El usuario no existe o no pertenece a tu sucursal." });

            var result = await _userService.UpdateUserAsync(id, userActualizado);
            if (!result.Success)
            {
                if (result.Message.Contains("no existe")) return NotFound(new { mensaje = result.Message });
                if (result.Message.Contains("no coincide")) return BadRequest(new { mensaje = result.Message });
                return StatusCode(500, new { error = "Error al editar", detalle = result.Message });
            }

            return NoContent();
        }

        // POST: api/Users
        [HttpPost]
        public async Task<ActionResult<User>> PostUser([FromBody] User user)
        {
            if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader))
                return BadRequest("Falta indicar la sucursal.");

            user.CompanyId = int.Parse(companyIdHeader!);

            var result = await _userService.CreateUserAsync(user, _env.ContentRootPath);
            if (!result.Success)
            {
                if (result.Message.Contains("SMTP no configurado"))
                    return BadRequest(result.Data);

                return BadRequest(result.Data);
            }

            return CreatedAtAction(nameof(GetUser), new { id = ((User)result.Data).Id }, result.Data);
        }

        // DELETE: api/Users/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader))
                return BadRequest("Falta indicar la sucursal.");
            int companyId = int.Parse(companyIdHeader!);

            var existingUser = await _userService.GetUserDtoByIdAsync(id);
            if (existingUser == null || existingUser.CompanyId != companyId)
                return NotFound();

            var success = await _userService.DeleteAsync(id);
            if (!success) return NotFound();

            return NoContent();
        }

        [HttpPost("Login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDTO request)
        {
            var result = await _userService.LoginAsync(request);

            if (!result.Success)
            {
                if (result.Requires2FA) return Unauthorized(new { requires2FA = true, mensaje = result.Message });
                if (result.AccountPending) return Unauthorized(new { accountPending = true, mensaje = result.Message });
                return Unauthorized(new { mensaje = result.Message });
            }

            if (result.RequirePasswordChange)
            {
                return Ok(new { requirePasswordChange = true, user = result.User });
            }

            return Ok(result.User);
        }

        [HttpPost("ChangeInitialPassword")]
        public async Task<IActionResult> ChangeInitialPassword([FromBody] ChangePasswordRequest request)
        {
            var result = await _userService.ChangeInitialPasswordAsync(request.UserId, request.NewPassword);
            if (!result.Success) return NotFound(new { mensaje = result.Message });

            return Ok(result.User);
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

            return Ok(new { Url = result.Url, mensaje = result.Message });
        }

        [HttpPost("{id}/generate-2fa")]
        public async Task<IActionResult> Generate2FA(int id)
        {
            var result = await _userService.Generate2FAAsync(id);
            if (!result.Success) return NotFound();

            return Ok(new { secret = result.Secret, qrUri = result.QrUri });
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

        // GET: api/Users/Approve/5
        [HttpGet("Approve/{id}")]
        public async Task<IActionResult> ApproveEmployee(int id)
        {
            var result = await _userService.ApproveEmployeeAsync(id);
            if (!result.Success)
                return Content("<h1>Error: Usuario no encontrado.</h1>", "text/html", System.Text.Encoding.UTF8);

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

            return Content(htmlBody, "text/html", System.Text.Encoding.UTF8);
        }

        [HttpPost("TestEmailConfiguration")]
        public async Task<IActionResult> TestEmailConfiguration([FromBody] SmtpTestRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                return BadRequest(new { mensaje = "El correo y la contraseña son obligatorios." });

            var result = await _userService.TestEmailConnectionAsync(request.Email, request.Password);
            if (!result.Success)
            {
                return StatusCode(500, new { error = "Fallo de conexión", detalle = result.Message });
            }

            return Ok(new { mensaje = result.Message });
        }
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