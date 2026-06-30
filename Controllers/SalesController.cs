using Microsoft.AspNetCore.Mvc;
using ControlInventario.Shared.Models;
using InventoryAPI.Data;
using System;
using System.Linq;
using System.Threading.Tasks;

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
                var empleado = await _context.Users.FindAsync(nuevaVenta.UserId);
                string nombreVendedor = empleado != null ? $"{empleado.FirstName} {empleado.LastName}" : "Usuario Desconocido";
                string nombreCliente = string.IsNullOrWhiteSpace(nuevaVenta.CustomerName) ? "Público General" : nuevaVenta.CustomerName;

                foreach (var detalle in nuevaVenta.SaleDetails)
                {
                    var articulo = await _context.Articles.FindAsync(detalle.ArticleId);

                    if (articulo == null) return NotFound($"Artículo {detalle.ArticleId} no existe.");
                    if (articulo.Stock < detalle.Quantity) return BadRequest($"Stock insuficiente para {articulo.Name}.");

                    articulo.Stock -= (decimal)detalle.Quantity;
                    articulo.ModificationDate = DateTime.Now;

                    var nuevoMovimiento = new Movement
                    {
                        ArticleId = articulo.Id,
                        EmployeeId = nuevaVenta.UserId,
                        ActionId = 2, // Venta
                        MovementDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        Observation = nuevaVenta.Notes,
                        Amount = (double)detalle.Quantity,
                        SalePrice = (double)detalle.UnitPrice,
                        PaymentMethod = nuevaVenta.PaymentType.ToString(),
                        Recipient = nombreCliente
                    };
                    _context.Movements.Add(nuevoMovimiento);

                    var nuevoLog = new HistoryLog
                    {
                        LogDate = DateTime.Now,
                        Username = nombreVendedor,
                        ModuleName = "Ventas",
                        ActionName = "Venta",
                        Detail = $"Producto \"{articulo.Name}\" vendido por \"{nombreVendedor}\" el \"{DateTime.Now:dd/MM/yyyy HH:mm}\" a \"{nombreCliente}\""
                    };
                    _context.HistoryLogs.Add(nuevoLog);
                }

                _context.Sales.Add(nuevaVenta);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { Message = "Venta procesada con éxito, stock actualizado y movimientos registrados." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                var errorReal = ex.GetBaseException().Message;
                return StatusCode(500, $"Error crítico en el servidor: {errorReal}");
            }
        }
    }
}