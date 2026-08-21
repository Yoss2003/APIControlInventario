using Microsoft.AspNetCore.Mvc;
using InventoryAPI.Services.IServices;
using ControlInventario.Shared.Models;

namespace InventoryAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BrandsController(IBrandService brandService) : ControllerBase
    {
        private readonly IBrandService _brandService = brandService;

        [HttpGet]
        public async Task<IActionResult> GetBrands()
        {
            if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader)) return BadRequest("Falta indicar la sucursal.");
            int companyId = int.Parse(companyIdHeader!);
            return Ok(await _brandService.GetAllByCompanyIdAsync(companyId));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetBrand(int id)
        {
            if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader)) return BadRequest("Falta indicar la sucursal.");
            int companyId = int.Parse(companyIdHeader!);

            var brand = await _brandService.GetByIdAsync(id);
            if (brand == null || brand.CompanyId != companyId) return NotFound($"No se encontró la marca.");

            return Ok(brand);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutBrand(int id, [FromBody] Brand brand)
        {
            if (id != brand.Id) return BadRequest("El ID no coincide.");
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader)) return BadRequest("Falta indicar la sucursal.");

            int companyId = int.Parse(companyIdHeader!);
            brand.CompanyId = companyId;

            var existingBrand = await _brandService.GetByIdAsync(id);
            if (existingBrand == null || existingBrand.CompanyId != companyId) return NotFound("Marca no encontrada.");

            var success = await _brandService.UpdateAsync(brand);
            if (!success) return BadRequest("No se pudo actualizar.");

            return NoContent();
        }

        [HttpPost]
        public async Task<IActionResult> PostBrand([FromBody] Brand brand)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader)) return BadRequest("Falta indicar la sucursal.");

            brand.CompanyId = int.Parse(companyIdHeader!);
            var success = await _brandService.CreateAsync(brand);
            if (!success) return BadRequest("No se pudo crear.");

            return CreatedAtAction(nameof(GetBrand), new { id = brand.Id }, brand);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBrand(int id)
        {
            if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader)) return BadRequest("Falta indicar la sucursal.");
            int companyId = int.Parse(companyIdHeader!);

            var existingBrand = await _brandService.GetByIdAsync(id);
            if (existingBrand == null || existingBrand.CompanyId != companyId) return NotFound("Marca no encontrada.");

            var success = await _brandService.DeleteAsync(id);
            if (!success) return BadRequest("No se pudo eliminar la marca.");

            return NoContent();
        }
    }
}