using ControlInventario.Shared.Models;
using InventoryAPI.Services.IServices;
using Microsoft.AspNetCore.Mvc;

namespace InventoryAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ArticlesController(IArticleService articleService) : ControllerBase
    {
        private readonly IArticleService _articleService = articleService;

        [HttpGet]
        public async Task<IActionResult> GetArticles()
        {
            try
            {
                // Cambiado de GetAllArticlesAsync a GetAllAsync
                var articles = await _articleService.GetAllAsync();
                return Ok(articles);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Error al obtener los artículos", detalle = ex.Message });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetArticle(int id)
        {
            try
            {
                // Cambiado de GetArticleByIdAsync a GetByIdAsync
                var article = await _articleService.GetByIdAsync(id);
                if (article == null) return NotFound();

                return Ok(article);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Error al obtener el artículo", detalle = ex.Message });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutArticle(int id, [FromBody] Article article)
        {
            if (id != article.Id) return BadRequest();
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                var success = await _articleService.UpdateAsync(article);

                if (!success) return NotFound(new { error = "El artículo no existe o no se pudo actualizar." });

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Error al actualizar el artículo", detalle = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> PostArticle([FromBody] Article article)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                // Cambiado de CreateArticleAsync a CreateAsync (ejecutará tu transacción personalizada en el servicio)
                var success = await _articleService.CreateAsync(article);
                if (!success) return BadRequest("No se pudo crear el artículo.");

                return CreatedAtAction(nameof(GetArticle), new { id = article.Id }, article);
            }
            catch (Exception ex)
            {
                var innerMessage = ex.GetBaseException().Message;
                return StatusCode(500, new { error = "Error crítico en Base de Datos", detalle = innerMessage });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteArticle(int id)
        {
            try
            {
                var existingArticle = await _articleService.GetByIdAsync(id);
                if (existingArticle == null) return NotFound();

                // Cambiado de DeleteArticleAsync a DeleteAsync
                var success = await _articleService.DeleteAsync(id);
                if (!success) return BadRequest("No se pudo eliminar el artículo.");

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Error al eliminar el artículo", detalle = ex.Message });
            }
        }

        [HttpGet("count/inventory/{inventoryId}")]
        public async Task<IActionResult> GetArticleCount(int inventoryId)
        {
            try
            {
                // Este método es personalizado tuyo, se mantiene intacto
                var totalUnidades = await _articleService.GetArticleCountByInventoryIdAsync(inventoryId);
                return Ok(totalUnidades);
            }
            catch (Exception)
            {
                return Ok(0);
            }
        }

        [HttpGet("barcode/{barcode}")]
        public async Task<IActionResult> GetArticleByBarcode(string barcode)
        {
            if (string.IsNullOrWhiteSpace(barcode))
            {
                return BadRequest(new { error = "El código de barras no puede estar vacío." });
            }

            try
            {
                // Este método también es personalizado y se mantiene intacto
                var articulo = await _articleService.GetArticleByBarcodeAsync(barcode);

                if (articulo == null)
                {
                    return NotFound(new { error = $"El código {barcode} no está registrado." });
                }

                return Ok(articulo);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Error al buscar por código de barras", detalle = ex.Message });
            }
        }
    }
}