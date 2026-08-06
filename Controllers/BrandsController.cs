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

        // GET: api/Brands
        [HttpGet]
        public async Task<IActionResult> GetBrands()
        {
            try
            {
                var brands = await _brandService.GetAllAsync();
                return Ok(brands);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno del servidor al recuperar las marcas: {ex.Message}");
            }
        }

        // GET: api/Brands/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetBrand(int id)
        {
            try
            {
                var brand = await _brandService.GetByIdAsync(id);
                if (brand == null)
                {
                    return NotFound($"No se encontró la marca con ID {id}.");
                }
                return Ok(brand);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno del servidor al recuperar la marca: {ex.Message}");
            }
        }

        // PUT: api/Brands/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutBrand(int id, [FromBody] Brand brand)
        {
            if (id != brand.Id)
            {
                return BadRequest("El ID de la URL no coincide con el ID de la marca proporcionada.");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var existingBrand = await _brandService.GetByIdAsync(id);
                if (existingBrand == null)
                {
                    return NotFound($"No se encontró la marca con ID {id} para actualizar.");
                }

                var success = await _brandService.UpdateAsync(brand);
                if (!success)
                {
                    return BadRequest("No se pudo actualizar la marca.");
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno del servidor al actualizar la marca: {ex.Message}");
            }
        }

        // POST: api/Brands
        [HttpPost]
        public async Task<IActionResult> PostBrand([FromBody] Brand brand)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var success = await _brandService.CreateAsync(brand);
                if (!success)
                {
                    return BadRequest("No se pudo crear la marca.");
                }

                return CreatedAtAction(nameof(GetBrand), new { id = brand.Id }, brand);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno del servidor al crear la marca: {ex.Message}");
            }
        }

        // DELETE: api/Brands/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBrand(int id)
        {
            try
            {
                var existingBrand = await _brandService.GetByIdAsync(id);
                if (existingBrand == null)
                {
                    return NotFound($"No se encontró la marca con ID {id} para eliminar.");
                }

                var success = await _brandService.DeleteAsync(id);
                if (!success)
                {
                    return BadRequest("No se pudo eliminar la marca.");
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno del servidor al eliminar la marca: {ex.Message}");
            }
        }
    }
}