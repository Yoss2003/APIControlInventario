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
            if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader)) 
                return BadRequest("Falta indicar la sucursal.");

            int companyId = int.Parse(companyIdHeader!);

            var profiles = await _profileService.GetAllByCompanyIdAsync(companyId);
            return Ok(profiles);
        }

        // GET: api/Profiles/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetProfile(int id)
        {
            if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader))
                return BadRequest("Falta indicar la sucursal.");

            int companyId = int.Parse(companyIdHeader!);
            var profile = await _profileService.GetByIdAsync(id);

            if (profile == null || profile.CompanyId != companyId)
                return NotFound();

            return Ok(profile);
        }

        // PUT: api/Profiles/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutProfile(int id, [FromBody] Profile profile)
        {
            if (id != profile.Id) 
                return BadRequest(new { error = "El ID no coincide." });

            if (!ModelState.IsValid) 
                return BadRequest(ModelState);

            if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader))
                return BadRequest("Falta indicar la sucursal.");

            int companyId = int.Parse(companyIdHeader!);

            try
            {
                profile.CompanyId = companyId;

                var existingProfile = await _profileService.GetByIdAsync(id);
                if (existingProfile == null || existingProfile.CompanyId != companyId)
                    return NotFound(new { error = "Registro no encontrado o no pertenece a tu sucursal." });

                var success = await _profileService.UpdateAsync(profile);

                if (!success) 
                    return BadRequest(new { error = "No se pudo actualizar." });

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Error interno", detalle = ex.InnerException?.Message ?? ex.Message });
            }
        }

        // POST: api/Profiles
        [HttpPost]
        public async Task<IActionResult> PostProfile([FromBody] Profile profile)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            if (Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader))
                profile.CompanyId = int.Parse(companyIdHeader!);

            var success = await _profileService.CreateAsync(profile);
            if (!success) return BadRequest("No se pudo crear.");
            return CreatedAtAction(nameof(GetProfile), new { id = profile.Id }, profile);
        }
    }
}