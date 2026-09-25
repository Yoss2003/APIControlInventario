using Microsoft.AspNetCore.Mvc;
using InventoryAPI.Services.IServices;
using ControlInventario.Shared.Models;

namespace InventoryAPI.Controllers
{
    public class CategoriesController(ICategoryService categoryService) : BaseApiController
    {
        private readonly ICategoryService _categoryService = categoryService;

        [HttpGet]
        public async Task<IActionResult> GetCategories()
        {
            int companyId = ObtenerEmpresaSegura();
            return Ok(await _categoryService.GetAllByCompanyIdAsync(companyId));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetCategory(int id)
        {
            int companyId = ObtenerEmpresaSegura();
            var category = await _categoryService.GetByIdAsync(id);

            if (category == null || category.CompanyId != companyId) return NotFound();
            return Ok(category);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutCategory(int id, [FromBody] Category category)
        {
            if (id != category.Id) return BadRequest("El ID no coincide.");
            if (!ModelState.IsValid) return BadRequest(ModelState);

            int companyId = ObtenerEmpresaSegura();
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

            category.CompanyId = ObtenerEmpresaSegura();
            var (Success, ErrorMessage) = await _categoryService.CreateCategoryAsync(category);

            if (!Success) return BadRequest(new { error = ErrorMessage });
            return CreatedAtAction(nameof(GetCategory), new { id = category.Id }, category);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            int companyId = ObtenerEmpresaSegura();
            string deletedBy = ObtenerUsuarioSeguro().ToString();

            var existingCategory = await _categoryService.GetByIdAsync(id);
            if (existingCategory == null || existingCategory.CompanyId != companyId) return NotFound();

            var (Success, Message) = await _categoryService.DeleteCategoryAsync(id, deletedBy);
            if (!Success) return BadRequest(new { error = Message });

            return NoContent();
        }
    }
}