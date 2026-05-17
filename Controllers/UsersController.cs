using InventoryAPI.Data;
using InventoryAPI.Models;
using InventoryAPI.Models.DTO;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
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
                    Email = u.Email,
                    Username = u.Username,
                    Age = u.Age,
                    BirthDate = u.BirthDate,
                    HireDate = u.HireDate,
                    PhoneNumber = u.PhoneNumber,
                    ProfilePictureUrl = u.ProfilePictureUrl,
                    IsActive = u.IsActive,
                    // Mapeos seguros controlando nulos
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
        [HttpPut("{id}")]
        public async Task<IActionResult> PutUser(int id, [FromBody] UserDTO userDto)
        {
            // 1. Verificamos que el ID de la URL coincida con el del cuerpo (body)
            if (id != userDto.Id)
            {
                return BadRequest(new { mensaje = "El ID de la ruta no coincide con el usuario." });
            }

            // 2. Buscamos al usuario REAL en la base de datos
            var userDb = await _context.Users.FindAsync(id);

            if (userDb == null)
            {
                return NotFound(new { mensaje = "El usuario no existe." });
            }

            // 3. ACTUALIZAMOS SOLAMENTE LOS CAMPOS PERMITIDOS
            userDb.FirstName = userDto.FirstName;
            userDb.LastName = userDto.LastName;
            userDb.PhoneNumber = userDto.PhoneNumber;

            // Si en el futuro agregas la edición de foto desde la app, descomentas esta línea:
            userDb.ProfilePictureUrl = userDto.ProfilePictureUrl;

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
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
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
        public async Task<ActionResult<UserDTO>> Login([FromBody] LoginRequestDTO request)
        {
            var userDto = await _context.Users
                .Where(u => u.Username == request.Username && u.Password == request.Password)
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

            // 2. Si es null, significa que las credenciales no coincidieron
            if (userDto == null)
            {
                // Devolvemos un código HTTP 401 (No Autorizado)
                return Unauthorized(new { mensaje = "Usuario o contraseña incorrectos" });
            }

            // 3. Si todo está correcto, devolvemos HTTP 200 (OK) con todos los datos del perfil
            return Ok(userDto);
        }

        private bool UserExists(int id)
        {
            return _context.Users.Any(e => e.Id == id);
        }

        [HttpPost("UploadPhoto")]
        public async Task<IActionResult> UploadPhoto(IFormFile file)
        {
            // Validamos que venga un archivo
            if (file == null || file.Length == 0)
                return BadRequest(new { mensaje = "No se recibió ninguna imagen." });

            // Creamos la ruta física donde se guardará (wwwroot/images/profiles)
            var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "profiles");

            // Si la carpeta no existe, la creamos
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            // Generamos un nombre único para que no se sobreescriban fotos con el mismo nombre
            var fileName = $"{Guid.NewGuid()}_{file.FileName}";
            var filePath = Path.Combine(folderPath, fileName);

            // Guardamos el archivo físicamente en el servidor
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // OJO: Cambia esta URL base por la de tu servidor real en Somee
            var fileUrl = $"http://db-inventario-api.somee.com/images/profiles/{fileName}";

            return Ok(new { Url = fileUrl });
        }
    }
}
