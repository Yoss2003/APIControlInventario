using Microsoft.AspNetCore.Mvc;
using InventoryAPI.Services.IServices;
using ControlInventario.Shared.Models;

namespace InventoryAPI.Controllers
{
    public class ProfilesController(IProfileService profileService) : BaseApiController
    {
        private readonly IProfileService _profileService = profileService;

        [HttpGet]
        public async Task<IActionResult> GetProfiles()
        {
            int companyId = ObtenerEmpresaSegura();
            var profiles = await _profileService.GetAllByCompanyIdAsync(companyId);
            return Ok(profiles);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetProfile(int id)
        {
            int companyId = ObtenerEmpresaSegura();
            var profile = await _profileService.GetByIdAsync(id);

            if (profile == null || profile.CompanyId != companyId) return NotFound();

            return Ok(profile);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutProfile(int id, [FromBody] Profile profile)
        {
            if (id != profile.Id) return BadRequest(new { error = "El ID no coincide." });
            if (!ModelState.IsValid) return BadRequest(ModelState);

            int companyId = ObtenerEmpresaSegura();

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
                existingProfile.QrBilletera = profile.QrBilletera;

                var success = await _profileService.UpdateAsync(existingProfile);
                if (!success) return BadRequest(new { error = "No se pudo actualizar." });

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Error interno", detalle = ex.InnerException?.Message ?? ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> PostProfile([FromBody] Profile profile)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            profile.CompanyId = ObtenerEmpresaSegura();

            var success = await _profileService.CreateAsync(profile);
            if (!success) return BadRequest("No se pudo crear.");

            return CreatedAtAction(nameof(GetProfile), new { id = profile.Id }, profile);
        }

        [HttpGet("user/{username}")]
        public async Task<IActionResult> GetProfileByUsername(string username)
        {
            int companyId = ObtenerEmpresaSegura();

            var profiles = await _profileService.GetAllByCompanyIdAsync(companyId);
            var profile = profiles.FirstOrDefault(p => p.Username.Equals(username, StringComparison.OrdinalIgnoreCase));

            if (profile == null) return NotFound();
            return Ok(profile);
        }
    }
}