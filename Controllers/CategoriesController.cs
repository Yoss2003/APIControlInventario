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

            var (Success, ErrorMessage) = await _categoryService.UpdateCategoryAsync(id, category);
            if (!Success) return BadRequest(new { error = ErrorMessage });

            return NoContent();
        }

        [HttpPost]
        public async Task<IActionResult> PostCategory([FromBody] Category category)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader)) return BadRequest("Falta indicar la sucursal.");

            category.CompanyId = int.Parse(companyIdHeader!);
            var (Success, ErrorMessage) = await _categoryService.CreateCategoryAsync(category);
            if (!Success) return BadRequest(new { error = ErrorMessage });

            return CreatedAtAction(nameof(GetCategory), new { id = category.Id }, category);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader)) return BadRequest("Falta indicar la sucursal.");
            int companyId = int.Parse(companyIdHeader!);

            string deletedBy = Request.Headers.TryGetValue("X-User-Name", out var userHeader) ? userHeader.ToString() : "Usuario Desconocido";

            var existingCategory = await _categoryService.GetByIdAsync(id);
            if (existingCategory == null || existingCategory.CompanyId != companyId) return NotFound();

            var (Success, Message) = await _categoryService.DeleteCategoryAsync(id, deletedBy);
            if (!Success) return BadRequest(new { error = Message });
            return NoContent();
        }
    }
}