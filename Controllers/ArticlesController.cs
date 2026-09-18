using ControlInventario.Shared.Models;
using InventoryAPI.Services.IServices;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.Design;

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
            if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader)) return BadRequest(new { error = "Falta el identificador de sucursal." });
            int companyId = int.Parse(companyIdHeader!);
            return Ok(await _articleService.GetAllByCompanyIdAsync(companyId));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetArticle(int id)
        {
            if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader)) return BadRequest("Falta indicar la sucursal.");
            int companyId = int.Parse(companyIdHeader!);

            var article = await _articleService.GetByIdAsync(id);
            if (article == null || article.CompanyId != companyId) return NotFound();

            return Ok(article);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutArticle(int id, [FromBody] Article article)
        {
            if (id != article.Id) return BadRequest();
            if (!ModelState.IsValid) return BadRequest(ModelState);

            if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader))
                return BadRequest("Falta indicar la sucursal.");

            int companyId = int.Parse(companyIdHeader!);
            article.CompanyId = companyId;

            var success = await _articleService.UpdateArticleWithAuditAsync(id, article, companyId);

            if (!success)
                return BadRequest(new { error = "El artículo no existe o no se pudo actualizar." });

            return NoContent();
        }

        [HttpPost]
        public async Task<IActionResult> PostArticle([FromBody] Article article)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader)) return BadRequest("Falta indicar la sucursal.");

            article.CompanyId = int.Parse(companyIdHeader!);

            var success = await _articleService.CreateAsync(article);
            if (!success) return BadRequest("No se pudo crear el artículo.");

            return CreatedAtAction(nameof(GetArticle), new { id = article.Id }, article);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteArticle(int id)
        {
            if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader)) return BadRequest("Falta indicar la sucursal.");
            int companyId = int.Parse(companyIdHeader!);

            string deletedBy = Request.Headers.TryGetValue("X-User-Name", out var userHeader) ? userHeader.ToString() : "Usuario Desconocido";

            var existingArticle = await _articleService.GetByIdAsync(id);
            if (existingArticle == null || existingArticle.CompanyId != companyId) return NotFound();

            var success = await _articleService.DeleteAsync(id, deletedBy);

            if (!success) return BadRequest("No se pudo eliminar el artículo.");
            return NoContent();
        }

        [HttpGet("count/inventory/{inventoryId}")]
        public async Task<IActionResult> GetArticleCount(int inventoryId)
        {
            // Nota: Si un inventario pertenece a una empresa, este conteo es seguro.
            try { return Ok(await _articleService.GetArticleCountByInventoryIdAsync(inventoryId)); }
            catch { return Ok(0); }
        }

        [HttpGet("barcode/{barcode}")]
        public async Task<IActionResult> GetArticleByBarcode(string barcode)
        {
            if (string.IsNullOrWhiteSpace(barcode)) return BadRequest(new { error = "El código de barras no puede estar vacío." });
            if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader)) return BadRequest("Falta indicar la sucursal.");
            int companyId = int.Parse(companyIdHeader!);

            var articulo = await _articleService.GetArticleByBarcodeAsync(barcode);
            if (articulo == null || articulo.CompanyId != companyId) return NotFound(new { error = $"El código {barcode} no está registrado en tu sucursal." });

            return Ok(articulo);
        }

        [HttpPost("AddDetail")]
        public async Task<IActionResult> PostArticleDetail([FromBody] ArticleDetails detail)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            if (!Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader))
                return BadRequest(new { error = "Falta indicar la sucursal." });

            int companyId = int.Parse(companyIdHeader!);
            var padre = await _articleService.GetByIdAsync(detail.ArticleId);

            if (padre == null || padre.CompanyId != companyId)
                return NotFound(new { error = "El artículo principal no existe o no pertenece a tu sucursal." });

            var (Success, ErrorMessage) = await _articleService.AddDetailAsync(detail);

            if (!Success)
                return BadRequest(new { error = $"Error BD: {ErrorMessage}" });

            // 🚀 LA MAGIA: Devolvemos el newId directamente desde la base de datos
            return Ok(new { message = "Número de serie registrado correctamente.", newId = detail.Id });
        }

        [HttpPut("UpdateDetail/{id}")]
        public async Task<IActionResult> UpdateDetail(int id, [FromBody] ArticleDetails detail)
        {
            if (id != detail.Id) return BadRequest();
            if (!ModelState.IsValid) return BadRequest(ModelState);

            if (!Request.Headers.TryGetValue("X-Company-Id", out _))
                return BadRequest("Falta indicar la sucursal.");

            var success = await _articleService.UpdateDetailAsync(detail);

            if (!success) return BadRequest(new { error = "No se pudo actualizar el número de serie." });

            return Ok(new { message = "Serie actualizada correctamente." });
        }

        [HttpDelete("DeleteDetail/{id}")]
        public async Task<IActionResult> DeleteDetail(int id)
        {
            if (!Request.Headers.TryGetValue("X-Company-Id", out _)) return BadRequest("Falta indicar la sucursal.");

            var success = await _articleService.DeleteDetailAsync(id);
            if (!success) return BadRequest(new { error = "No se pudo dar de baja la serie." });

            return Ok(new { message = "Serie dada de baja correctamente." });
        }
    }
}