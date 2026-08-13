using Microsoft.AspNetCore.Mvc;
using InventoryAPI.Services.IServices;
using ControlInventario.Shared.Models;

namespace InventoryAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProfilesController(IProfileService profileService) : ControllerBase
    {
        private readonly IProfileService _profileService = profileService;

        // GET: api/Profiles
        [HttpGet]
        public async Task<IActionResult> GetProfiles()
        {
            var profiles = await _profileService.GetAllAsync();
            return Ok(profiles);
        }

        // GET: api/Profiles/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetProfile(int id)
        {
            var profile = await _profileService.GetByIdAsync(id);

            if (profile == null)
            {
                return NotFound();
            }

            return Ok(profile);
        }

        // PUT: api/Profiles/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutProfile(int id, [FromBody] Profile profile)
        {
            if (id != profile.Id)
            {
                return BadRequest(new { error = "El ID no coincide." });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var success = await _profileService.UpdateAsync(profile);

                if (!success)
                {
                    return BadRequest(new { error = "No se pudo actualizar el perfil." });
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Error interno del servidor", detalle = ex.InnerException?.Message ?? ex.Message });
            }
        }

        // POST: api/Profiles
        [HttpPost]
        public async Task<IActionResult> PostProfile([FromBody] Profile profile)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var success = await _profileService.CreateAsync(profile);
            if (!success)
            {
                return BadRequest("No se pudo crear el perfil.");
            }

            return CreatedAtAction(nameof(GetProfile), new { id = profile.Id }, profile);
        }

        // DELETE: api/Profiles/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProfile(int id)
        {
            var existingProfile = await _profileService.GetByIdAsync(id);
            if (existingProfile == null)
            {
                return NotFound();
            }

            var success = await _profileService.DeleteAsync(id);
            if (!success)
            {
                return BadRequest("No se pudo eliminar el perfil.");
            }

            return NoContent();
        }
    }
}