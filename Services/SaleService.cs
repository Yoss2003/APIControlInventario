using ControlInventario.Shared.Models;
using InventoryAPI.Repositories;
using InventoryAPI.Repositories.IRepositories;
using InventoryAPI.Services.IServices;

namespace InventoryAPI.Services
{
    public class SaleService(IWorkFlow workFlow) : WorkContainer<Sale>(workFlow), ISaleService
    {
        public async Task<(bool Success, string Message)> ProcessSaleAsync(Sale nuevaVenta)
        {
            if (nuevaVenta == null || nuevaVenta.SaleDetails == null || !nuevaVenta.SaleDetails.Any())
            {
                return (false, "Datos de venta inválidos o carrito vacío.");
            }

            try
            {
                nuevaVenta.SaleDate = DateTime.Now;

                // 1. Recuperar al vendedor. Como no podemos usar .Include directo en el repo genérico básico,
                // traemos al usuario y luego buscamos sus datos de empleado.
                var userMatch = await _workFlow.Repository<User>().FindAsync(u => u.Id == nuevaVenta.UserId);
                var vendedorUser = userMatch.FirstOrDefault();

                var empMatch = await _workFlow.Repository<Employee>().FindAsync(e => e.UserId == nuevaVenta.UserId || e.Id == nuevaVenta.UserId);
                var vendedorEmpleado = empMatch.FirstOrDefault();

                string nombreVendedor = vendedorEmpleado != null
                    ? $"{vendedorEmpleado.FirstName} {vendedorEmpleado.LastName}".Trim()
                    : (vendedorUser?.Username ?? "Usuario Desconocido");

                string nombreCliente = string.IsNullOrWhiteSpace(nuevaVenta.CustomerName)
                    ? "Público General"
                    : nuevaVenta.CustomerName;

                // 2. Procesar cada detalle de la venta
                foreach (var detalle in nuevaVenta.SaleDetails)
                {
                    var articulo = await _workFlow.Repository<Article>().GetByIdAsync(detalle.ArticleId);

                    if (articulo == null) return (false, $"Artículo {detalle.ArticleId} no existe.");
                    if (articulo.Stock < detalle.Quantity) return (false, $"Stock insuficiente para {articulo.Name}.");

                    // 2.1 Actualizar Stock
                    articulo.Stock -= (decimal)detalle.Quantity;
                    articulo.ModificationDate = DateTime.Now;
                    // Al estar en memoria, EF Core rastrea este cambio de stock automáticamente

                    // 2.2 Crear Movimiento
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
                    await _workFlow.Repository<Movement>().AddAsync(nuevoMovimiento);

                    // 2.3 Crear Log de Auditoría
                    var nuevoLog = new HistoryLog
                    {
                        LogDate = DateTime.Now,
                        Username = nombreVendedor,
                        ModuleName = "Ventas",
                        ActionName = "Venta",
                        Detail = $"Producto \"{articulo.Name}\" vendido por \"{nombreVendedor}\" el \"{DateTime.Now:dd/MM/yyyy HH:mm}\" a \"{nombreCliente}\""
                    };
                    await _workFlow.Repository<HistoryLog>().AddAsync(nuevoLog);
                }

                // 3. Agregar la cabecera de la Venta
                await _workFlow.Repository<Sale>().AddAsync(nuevaVenta);

                // 4. EL TRUCO MÁGICO: CompleteAsync hace un solo SaveChangesAsync.
                // Si algo falla antes de esta línea, NO SE GUARDA NADA (Transacción implícita exitosa).
                await _workFlow.CompleteAsync();

                return (true, "Venta procesada con éxito, stock actualizado y movimientos registrados.");
            }
            catch (Exception ex)
            {
                var errorReal = ex.GetBaseException().Message;
                return (false, $"Error crítico en el servidor: {errorReal}");
            }
        }
    }
}