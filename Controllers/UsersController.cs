using ControlInventario.Shared.Models;
using InventoryAPI.Data;
using InventoryAPI.Models.DTO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OtpNet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace InventoryAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly AppDbContext _context;

        public UsersController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/Users
        [HttpGet]
        public async Task<ActionResult<IEnumerable<UserDTO>>> GetUsers()
        {
            return await _context.Users
                .Select(u => new UserDTO
                {
                    Id = u.Id,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    Email = u.Email ?? "",
                    Username = u.Username,
                    Age = u.Age ?? 0,
                    BirthDate = u.BirthDate ?? "",
                    HireDate = u.HireDate ?? "",
                    PhoneNumber = u.PhoneNumber ?? "",
                    ProfilePictureUrl = u.ProfilePictureUrl ?? "",
                    IsActive = u.IsActive,
                    RoleName = u.Role != null ? u.Role.Name : "Usuario",
                    JobPositionId = u.JobPositionId ?? 0,
                    AreaId = u.AreaId ?? 0,
                    ContractTypeId = u.ContractTypeId ?? 0
                })
                .ToListAsync();
        }

        // GET: api/Users/5
        [HttpGet("{id}")]
        public async Task<ActionResult<UserDTO>> GetUser(int id)
        {
            var userDto = await _context.Users
                .Where(u => u.Id == id)
                .Select(u => new UserDTO
                {
                    Id = u.Id,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    Email = u.Email ?? "",
                    Username = u.Username,
                    Age = u.Age ?? 0,
                    BirthDate = u.BirthDate ?? "",
                    HireDate = u.HireDate ?? "",
                    PhoneNumber = u.PhoneNumber ?? "",
                    ProfilePictureUrl = u.ProfilePictureUrl ?? "",
                    IsActive = u.IsActive,
                    RoleName = u.Role != null ? u.Role.Name : "Usuario",
                    JobPositionId = u.JobPositionId ?? 0,
                    AreaId = u.AreaId ?? 0,
                    ContractTypeId = u.ContractTypeId ?? 0
                })
                .FirstOrDefaultAsync();

            if (userDto == null) return NotFound(new { mensaje = "El usuario no fue localizado." });

            return userDto;
        }

        // PUT: api/Users/5
        // 🛠️ CORRECCIÓN: Ahora recibe 'User' en lugar de 'UserDTO' para hacer match perfecto con la app MAUI
        [HttpPut("{id}")]
        public async Task<IActionResult> PutUser(int id, [FromBody] User userActualizado)
        {
            if (id != userActualizado.Id) return BadRequest(new { mensaje = "El ID no coincide." });

            var userDb = await _context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == id);
            if (userDb == null) return NotFound(new { mensaje = "El usuario no existe." });

            userDb.FirstName = userActualizado.FirstName;
            userDb.LastName = userActualizado.LastName;
            userDb.Email = userActualizado.Email;
            userDb.Username = userActualizado.Username;
            userDb.PhoneNumber = userActualizado.PhoneNumber;
            userDb.ProfilePictureUrl = userActualizado.ProfilePictureUrl;
            userDb.IsActive = userActualizado.IsActive;

            // 🛡️ Proteger las llaves foráneas: Solo actualizamos si la app envía un ID mayor a 0
            if (userActualizado.AreaId > 0) userDb.AreaId = userActualizado.AreaId;
            if (userActualizado.JobPositionId > 0) userDb.JobPositionId = userActualizado.JobPositionId;
            if (userActualizado.ContractTypeId > 0) userDb.ContractTypeId = userActualizado.ContractTypeId;

            if (userActualizado.RoleId > 0) userDb.RoleId = userActualizado.RoleId;

            if (!string.IsNullOrWhiteSpace(userActualizado.Password))
                userDb.Password = userActualizado.Password;

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
            // 🛡️ SALVAVIDAS FK: Como SQL Server bloqueó el cambio a NULL, 
            // inyectamos el ID 1 por defecto para que la base de datos no arroje Error 500.
            user.JobPositionId = (user.JobPositionId == null || user.JobPositionId <= 0) ? 1 : user.JobPositionId;
            user.AreaId = (user.AreaId == null || user.AreaId <= 0) ? 1 : user.AreaId;
            user.ContractTypeId = (user.ContractTypeId == null || user.ContractTypeId <= 0) ? 1 : user.ContractTypeId;

            user.Age = user.Age ?? 0;
            user.Role = null; // Evita error de roles duplicados en EF

            try
            {
                _context.Users.Add(user);
                await _context.SaveChangesAsync();
                return CreatedAtAction("GetUser", new { id = user.Id }, user);
            }
            catch (Exception ex)
            {
                // Si SQL Server lo rechaza, te imprimirá EXACTAMENTE la razón
                System.Diagnostics.Debug.WriteLine($"[CRÍTICO SQL]: {ex.InnerException?.Message ?? ex.Message}");
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
        public async Task<ActionResult<User>> Login([FromBody] LoginRequestDTO request)
        {
            var user = await _context.Users
            .Include(u => u.Role)
            .ThenInclude(r => r.RolePermissions!)
            .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(u => u.Username == request.Username && u.Password == request.Password);

            if (user == null) return Unauthorized(new { mensaje = "Usuario o contraseña incorrectos" });

            if (user.IsTwoFactorEnabled)
            {
                if (string.IsNullOrWhiteSpace(request.TwoFactorCode))
                    return Unauthorized(new { requires2FA = true, mensaje = "Código 2FA requerido" });

                var secretBytes = Base32Encoding.ToBytes(user.TwoFactorSecret);
                var totp = new Totp(secretBytes);
                bool isValid = totp.VerifyTotp(request.TwoFactorCode, out long timeStepMatched, window: new VerificationWindow(2, 2));

                if (!isValid) return Unauthorized(new { mensaje = "El código de seguridad es incorrecto o ha expirado." });
            }

            return Ok(user);
        }

        [HttpPost("UploadPhoto")]
        public async Task<IActionResult> UploadPhoto(IFormFile file)
        {
            if (file == null || file.Length == 0) return BadRequest(new { mensaje = "No imagen." });
            var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "profiles");
            if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);
            var fileName = $"{Guid.NewGuid()}_{file.FileName}";
            var filePath = Path.Combine(folderPath, fileName);
            using (var stream = new FileStream(filePath, FileMode.Create)) await file.CopyToAsync(stream);
            var fileUrl = $"http://db-inventario-api.somee.com/images/profiles/{fileName}";
            return Ok(new { Url = fileUrl });
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
            return Ok(new { secret = secret, qrUri = qrUri });
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
    }
}