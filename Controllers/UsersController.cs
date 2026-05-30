using ControlInventario.Shared.Models;
using InventoryAPI.Data;
using InventoryAPI.Models.DTO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
                    Email = u.Email,
                    Username = u.Username,
                    Age = u.Age,
                    BirthDate = u.BirthDate,
                    HireDate = u.HireDate,
                    PhoneNumber = u.PhoneNumber,
                    ProfilePictureUrl = u.ProfilePictureUrl,
                    IsActive = u.IsActive,
                    RoleName = u.Role != null ? u.Role.Name : "Usuario",
                    JobPositionId = u.JobPositionId,
                    AreaId = u.AreaId,
                    ContractTypeId = u.ContractTypeId
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
                    Email = u.Email,
                    Username = u.Username,
                    Age = u.Age,
                    BirthDate = u.BirthDate,
                    HireDate = u.HireDate,
                    PhoneNumber = u.PhoneNumber,
                    ProfilePictureUrl = u.ProfilePictureUrl,
                    IsActive = u.IsActive,
                    RoleName = u.Role != null ? u.Role.Name : "Usuario",
                    JobPositionId = u.JobPositionId,
                    AreaId = u.AreaId,
                    ContractTypeId = u.ContractTypeId
                })
                .FirstOrDefaultAsync();

            if (userDto == null)
            {
                return NotFound(new { mensaje = "El usuario no fue localizado." });
            }

            return userDto;
        }

        // PUT: api/Users/5
        // ACTUALIZADO CON TODOS LOS CAMPOS DEL SÚPER ADMINISTRADOR
        [HttpPut("{id}")]
        public async Task<IActionResult> PutUser(int id, [FromBody] UserDTO userDto)
        {
            if (id != userDto.Id)
            {
                return BadRequest(new { mensaje = "El ID de la ruta no coincide con el usuario." });
            }

            // Buscamos al usuario e incluimos su Rol para poder manipularlo
            var userDb = await _context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == id);

            if (userDb == null)
            {
                return NotFound(new { mensaje = "El usuario no existe." });
            }

            // 1. Actualización de Campos de Texto y Estado
            userDb.FirstName = userDto.FirstName;
            userDb.LastName = userDto.LastName;
            userDb.Email = userDto.Email;
            userDb.Username = userDto.Username;
            userDb.PhoneNumber = userDto.PhoneNumber;
            userDb.ProfilePictureUrl = userDto.ProfilePictureUrl;
            userDb.IsActive = userDto.IsActive;

            // 2. Actualización de los Ids de los Parámetros (Pickers de MAUI)
            userDb.AreaId = userDto.AreaId;
            userDb.JobPositionId = userDto.JobPositionId;
            userDb.ContractTypeId = userDto.ContractTypeId;

            // 3. Traducción de Nombre de Rol a RolId numérico
            if (userDto.RoleId > 0)
            {
                userDb.RoleId = userDto.RoleId;
            }
            else if (!string.IsNullOrWhiteSpace(userDto.RoleName))
            {
                var roleObj = await _context.Roles.FirstOrDefaultAsync(r => r.Name == userDto.RoleName);
                if (roleObj != null)
                {
                    userDb.RoleId = roleObj.Id;
                }
            }

            // 4. Actualización Opcional de Contraseña (Texto Plano acorde a tu Login)
            if (!string.IsNullOrWhiteSpace(userDto.Password))
            {
                userDb.Password = userDto.Password;
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!UserExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // POST: api/Users
        [HttpPost]
        public async Task<ActionResult<User>> PostUser(User user)
        {
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetUser", new { id = user.Id }, user);
        }

        // DELETE: api/Users/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpPost("Login")]
        public async Task<ActionResult<User>> Login([FromBody] LoginRequestDTO request)
        {
            // Buscamos al usuario incluyendo su Rol completo de la base de datos
            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Username == request.Username && u.Password == request.Password);

            if (user == null)
            {
                return Unauthorized(new { mensaje = "Usuario o contraseña incorrectos" });
            }

            return Ok(user);
        }

        private bool UserExists(int id)
        {
            return _context.Users.Any(e => e.Id == id);
        }

        [HttpPost("UploadPhoto")]
        public async Task<IActionResult> UploadPhoto(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { mensaje = "No se recibió ninguna imagen." });

            var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "profiles");

            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            var fileName = $"{Guid.NewGuid()}_{file.FileName}";
            var filePath = Path.Combine(folderPath, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var fileUrl = $"http://db-inventario-api.somee.com/images/profiles/{fileName}";

            return Ok(new { Url = fileUrl });
        }
    }
}