using Microsoft.AspNetCore.Mvc;
using InventoryAPI.Services.IServices;
using ControlInventario.Shared.Models;

namespace InventoryAPI.Controllers
{
    public class BrandsController(IBrandService brandService) : BaseApiController
    {
        private readonly IBrandService _brandService = brandService;

        [HttpGet]
        public async Task<IActionResult> GetBrands()
        {
            int companyId = ObtenerEmpresaSegura();
            return Ok(await _brandService.GetAllByCompanyIdAsync(companyId));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetBrand(int id)
        {
            int companyId = ObtenerEmpresaSegura();
            var brand = await _brandService.GetByIdAsync(id);

            if (brand == null || brand.CompanyId != companyId) return NotFound($"No se encontró la marca.");
            return Ok(brand);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutBrand(int id, [FromBody] Brand brand)
        {
            if (id != brand.Id) return BadRequest("El ID no coincide.");
            if (!ModelState.IsValid) return BadRequest(ModelState);

            int companyId = ObtenerEmpresaSegura();
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

            brand.CompanyId = ObtenerEmpresaSegura();
            var success = await _brandService.CreateAsync(brand);

            if (!success) return BadRequest("No se pudo crear.");
            return CreatedAtAction(nameof(GetBrand), new { id = brand.Id }, brand);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBrand(int id)
        {
            int companyId = ObtenerEmpresaSegura();
            string deletedBy = ObtenerUsuarioSeguro().ToString();

            var existingBrand = await _brandService.GetByIdAsync(id);
            if (existingBrand == null || existingBrand.CompanyId != companyId) return NotFound("Marca no encontrada.");

            var success = await _brandService.DeleteAsync(id, deletedBy);
            if (!success) return BadRequest("No se pudo eliminar la marca.");

            return NoContent();
        }
    }
}