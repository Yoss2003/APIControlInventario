using Microsoft.AspNetCore.Mvc;
using InventoryAPI.Services.IServices;
using ControlInventario.Shared.Models;

namespace InventoryAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CategoriesController(ICategoryService categoryService) : ControllerBase
    {
        private readonly ICategoryService _categoryService = categoryService;

        [HttpGet]
        public async Task<IActionResult> GetCategories()
        {
            if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader)) return BadRequest("Falta indicar la sucursal.");
            int companyId = int.Parse(companyIdHeader!);
            return Ok(await _categoryService.GetAllByCompanyIdAsync(companyId));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetCategory(int id)
        {
            if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader)) return BadRequest("Falta indicar la sucursal.");
            int companyId = int.Parse(companyIdHeader!);

            var category = await _categoryService.GetByIdAsync(id);
            if (category == null || category.CompanyId != companyId) return NotFound();

            return Ok(category);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutCategory(int id, [FromBody] Category category)
        {
            if (id != category.Id) return BadRequest("El ID no coincide.");
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader)) return BadRequest("Falta indicar la sucursal.");

            int companyId = int.Parse(companyIdHeader!);
            category.CompanyId = companyId;

            var existingCategory = await _categoryService.GetByIdAsync(id);
            if (existingCategory == null || existingCategory.CompanyId != companyId) return NotFound("Categoría no encontrada.");

            var result = await _categoryService.UpdateCategoryAsync(id, category);
            if (!result.Success) return BadRequest(new { error = result.Message });

            return NoContent();
        }

        [HttpPost]
        public async Task<IActionResult> PostCategory([FromBody] Category category)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader)) return BadRequest("Falta indicar la sucursal.");

            category.CompanyId = int.Parse(companyIdHeader!);
            var result = await _categoryService.CreateCategoryAsync(category);
            if (!result.Success) return BadRequest(new { error = result.Message });

            return CreatedAtAction(nameof(GetCategory), new { id = category.Id }, category);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader)) return BadRequest("Falta indicar la sucursal.");
            int companyId = int.Parse(companyIdHeader!);

            var existingCategory = await _categoryService.GetByIdAsync(id);
            if (existingCategory == null || existingCategory.CompanyId != companyId) return NotFound();

            var result = await _categoryService.DeleteCategoryAsync(id);
            if (!result.Success) return BadRequest(new { error = result.Message });

            return NoContent();
        }
    }
}