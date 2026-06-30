using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InventoryAPI.Data;
using ControlInventario.Shared.Models;

namespace InventoryAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ArticlesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ArticlesController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/Articles
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Article>>> GetArticles()
        {
            return await _context.Articles.ToListAsync();
        }

        // GET: api/Articles/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Article>> GetArticle(int id)
        {
            var article = await _context.Articles.FindAsync(id);

            if (article == null)
            {
                return NotFound();
            }

            return article;
        }

        // PUT: api/Articles/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutArticle(int id, Article article)
        {
            if (id != article.Id)
            {
                return BadRequest();
            }

            _context.Entry(article).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ArticleExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // POST: api/Articles
        [HttpPost]
        public async Task<ActionResult<Article>> PostArticle(Article article)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                _context.Articles.Add(article);
                await _context.SaveChangesAsync();

                var nuevoMovimiento = new Movement
                {
                    ArticleId = article.Id,
                    EmployeeId = article.LoggedUserId,
                    ActionId = 1,
                    MovementDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    Observation = "Registro inicial del producto en almacén",
                    Amount = (double)article.Stock,
                    SalePrice = (double)(article.SalePrice ?? 0m)
                };
                _context.Movements.Add(nuevoMovimiento);
                string nombreUsuario = string.IsNullOrWhiteSpace(article.LoggedUserFullName)
                                       ? "Usuario Desconocido"
                                       : article.LoggedUserFullName;

                var nuevoLog = new HistoryLog
                {
                    LogDate = DateTime.Now,
                    Username = nombreUsuario,
                    ModuleName = "Inventario",
                    ActionName = "Creación",
                    Detail = $"Producto \"{article.Name}\" agregado por \"{nombreUsuario}\" el \"{DateTime.Now:dd/MM/yyyy HH:mm}\""
                };
                _context.HistoryLogs.Add(nuevoLog);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return CreatedAtAction("GetArticle", new { id = article.Id }, article);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                var innerMessage = ex.GetBaseException().Message;
                return StatusCode(500, new { error = "Error crítico en Base de Datos", detalle = innerMessage });
            }
        }

        // DELETE: api/Articles/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteArticle(int id)
        {
            var article = await _context.Articles.FindAsync(id);
            if (article == null)
            {
                return NotFound();
            }

            _context.Articles.Remove(article);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // ====================================================================
        // 🌟 NUEVO ENDPOINT 1: SUMA DEL STOCK TOTAL POR INVENTARIO
        // ====================================================================
        [HttpGet("count/inventory/{inventoryId}")]
        public async Task<IActionResult> GetArticleCount(int inventoryId)
        {
            try
            {
                // ✔️ CAMBIO CLAVE: Usamos 'decimal' en lugar de 'double' para hacer match perfecto
                decimal stockTotal = await _context.Articles
                    .Where(a => a.InventoryId == inventoryId)
                    .SumAsync(a => a.Stock);

                // Convertimos a entero para renderizar números limpios en el Dashboard del celular
                int totalUnidades = (int)stockTotal;

                return Ok(totalUnidades);
            }
            catch (Exception)
            {
                return Ok(0); // Resguardo por si la tabla está vacía
            }
        }

        // ====================================================================
        // 🌟 NUEVO ENDPOINT 2: BÚSQUEDA POR CÓDIGO DE BARRAS (Para la Cámara)
        // ====================================================================
        [HttpGet("barcode/{barcode}")]
        public async Task<IActionResult> GetArticleByBarcode(string barcode)
        {
            if (string.IsNullOrWhiteSpace(barcode))
            {
                return BadRequest(new { error = "El código de barras no puede estar vacío." });
            }

            var articulo = await _context.Articles
                .FirstOrDefaultAsync(a => a.Barcode == barcode);

            if (articulo == null)
            {
                return NotFound(new { error = $"El código {barcode} no está registrado." });
            }

            return Ok(articulo);
        }

        private bool ArticleExists(int id)
        {
            return _context.Articles.Any(e => e.Id == id);
        }
    }
}