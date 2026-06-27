using Microsoft.AspNetCore.Mvc;
using ControlInventario.Shared.Models;
using InventoryAPI.Data;

namespace InventoryAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SalesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public SalesController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> CreateSale([FromBody] Sale nuevaVenta)
        {
            if (nuevaVenta == null || !nuevaVenta.SaleDetails.Any())
            {
                return BadRequest("Datos de venta inválidos o carrito vacío.");
            }

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                nuevaVenta.SaleDate = DateTime.Now;

                foreach (var detalle in nuevaVenta.SaleDetails)
                {
                    var articulo = await _context.Articles.FindAsync(detalle.ArticleId);

                    if (articulo == null)
                    {
                        return NotFound($"El artículo con ID {detalle.ArticleId} no existe en el inventario.");
                    }

                    if (articulo.Stock < detalle.Quantity)
                    {
                        return BadRequest($"Stock insuficiente para {articulo.Name}. Disponible: {articulo.Stock}, Solicitado: {detalle.Quantity}");
                    }

                    articulo.Stock -= (decimal)detalle.Quantity;
                    articulo.ModificationDate = DateTime.Now;
                }

                _context.Sales.Add(nuevaVenta);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                return Ok(new { Message = "Venta procesada con éxito y stock actualizado." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                var errorReal = ex.InnerException?.Message ?? ex.Message;
                return StatusCode(500, $"Error crítico en el servidor: {errorReal}");
            }
        }
    }
}