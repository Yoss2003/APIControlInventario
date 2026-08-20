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
                var existingProfile = await _profileService.GetByIdAsync(id);
                if (existingProfile == null || existingProfile.CompanyId != companyId)
                    return NotFound(new { error = "Registro no encontrado o no pertenece a tu sucursal." });

                existingProfile.LanguageId = profile.LanguageId;
                existingProfile.ThemeId = profile.ThemeId;
                existingProfile.NotificationId = profile.NotificationId;
                existingProfile.DateFormatId = profile.DateFormatId;
                existingProfile.CurrencyId = profile.CurrencyId;
                existingProfile.MeasurementUnitId = profile.MeasurementUnitId;
                existingProfile.TimeZoneId = profile.TimeZoneId;
                existingProfile.SalesModeId = profile.SalesModeId;

                existingProfile.UseAuthentication = profile.UseAuthentication;
                existingProfile.SharedActivity = profile.SharedActivity;
                existingProfile.UseBarcodes = profile.UseBarcodes;
                existingProfile.CalculateDevaluation = profile.CalculateDevaluation;
                existingProfile.GenerateCodes = profile.GenerateCodes;

                existingProfile.ApplyLateFee = profile.ApplyLateFee;
                existingProfile.LateFeePercentage = profile.LateFeePercentage;
                existingProfile.GraceDays = profile.GraceDays;

                existingProfile.SmtpEmail = profile.SmtpEmail;
                existingProfile.SmtpPassword = profile.SmtpPassword;
                existingProfile.SmtpApproverEmail = profile.SmtpApproverEmail;

                var success = await _profileService.UpdateAsync(existingProfile);

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

        // GET: api/Profiles/user
        [HttpGet("user/{username}")]
        public async Task<IActionResult> GetProfileByUsername(string username)
        {
            if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader))
                return BadRequest("Falta indicar la sucursal.");

            int companyId = int.Parse(companyIdHeader!);

            // Buscamos en el servicio filtrando por Username Y por CompanyId
            var profiles = await _profileService.GetAllByCompanyIdAsync(companyId);
            var profile = profiles.FirstOrDefault(p => p.Username.Equals(username, StringComparison.OrdinalIgnoreCase));

            if (profile == null) return NotFound();

            return Ok(profile);
        }
    }
}