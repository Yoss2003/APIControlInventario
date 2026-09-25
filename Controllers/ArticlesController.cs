using ControlInventario.Shared.Models;
using InventoryAPI.Services.IServices;
using Microsoft.AspNetCore.Mvc;

namespace InventoryAPI.Controllers
{
    public class ArticlesController(IArticleService articleService) : BaseApiController
    {
        private readonly IArticleService _articleService = articleService;

        [HttpGet]
        public async Task<IActionResult> GetArticles()
        {
            int companyId = ObtenerEmpresaSegura();
            return Ok(await _articleService.GetAllByCompanyIdAsync(companyId));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetArticle(int id)
        {
            int companyId = ObtenerEmpresaSegura();
            var article = await _articleService.GetByIdAsync(id);

            if (article == null || article.CompanyId != companyId) return NotFound();
            return Ok(article);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutArticle(int id, [FromBody] Article article)
        {
            if (id != article.Id) return BadRequest();
            if (!ModelState.IsValid) return BadRequest(ModelState);

            int companyId = ObtenerEmpresaSegura();
            article.CompanyId = companyId;

            var success = await _articleService.UpdateArticleWithAuditAsync(id, article, companyId);
            if (!success) return BadRequest(new { error = "El artículo no existe o no se pudo actualizar." });

            return NoContent();
        }

        [HttpPost]
        public async Task<IActionResult> PostArticle([FromBody] Article article)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            article.CompanyId = ObtenerEmpresaSegura();
            var success = await _articleService.CreateAsync(article);

            if (!success) return BadRequest("No se pudo crear el artículo.");
            return CreatedAtAction(nameof(GetArticle), new { id = article.Id }, article);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteArticle(int id)
        {
            int companyId = ObtenerEmpresaSegura();
            string deletedBy = ObtenerUsuarioSeguro().ToString();

            var existingArticle = await _articleService.GetByIdAsync(id);
            if (existingArticle == null || existingArticle.CompanyId != companyId) return NotFound();

            var success = await _articleService.DeleteAsync(id, deletedBy);
            if (!success) return BadRequest("No se pudo eliminar el artículo.");

            return NoContent();
        }

        [HttpGet("count/inventory/{inventoryId}")]
        public async Task<IActionResult> GetArticleCount(int inventoryId)
        {
            try { return Ok(await _articleService.GetArticleCountByInventoryIdAsync(inventoryId)); }
            catch { return Ok(0); }
        }

        [HttpGet("barcode/{barcode}")]
        public async Task<IActionResult> GetArticleByBarcode(string barcode)
        {
            if (string.IsNullOrWhiteSpace(barcode)) return BadRequest(new { error = "El código de barras no puede estar vacío." });

            int companyId = ObtenerEmpresaSegura();
            var articulo = await _articleService.GetArticleByBarcodeAsync(barcode);

            if (articulo == null || articulo.CompanyId != companyId)
                return NotFound(new { error = $"El código {barcode} no está registrado en tu sucursal." });

            return Ok(articulo);
        }

        [HttpPost("AddDetail")]
        public async Task<IActionResult> PostArticleDetail([FromBody] ArticleDetails detail)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            int companyId = ObtenerEmpresaSegura();
            var padre = await _articleService.GetByIdAsync(detail.ArticleId);

            if (padre == null || padre.CompanyId != companyId)
                return NotFound(new { error = "El artículo principal no existe o no pertenece a tu sucursal." });

            var (Success, ErrorMessage) = await _articleService.AddDetailAsync(detail);
            if (!Success) return BadRequest(new { error = $"Error BD: {ErrorMessage}" });

            return Ok(new { message = "Número de serie registrado correctamente.", newId = detail.Id });
        }

        [HttpPut("UpdateDetail/{id}")]
        public async Task<IActionResult> UpdateDetail(int id, [FromBody] ArticleDetails detail)
        {
            if (id != detail.Id) return BadRequest();
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var success = await _articleService.UpdateDetailAsync(detail);
            if (!success) return BadRequest(new { error = "No se pudo actualizar el número de serie." });

            return Ok(new { message = "Serie actualizada correctamente." });
        }

        [HttpDelete("DeleteDetail/{id}")]
        public async Task<IActionResult> DeleteDetail(int id)
        {
            var success = await _articleService.DeleteDetailAsync(id);
            if (!success) return BadRequest(new { error = "No se pudo dar de baja la serie." });

            return Ok(new { message = "Serie dada de baja correctamente." });
        }
    }
}