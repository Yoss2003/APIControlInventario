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

                var userMatch = await _workFlow.Repository<User>().FindAsync(u => u.Id == nuevaVenta.UserId);
                var vendedorUser = userMatch.FirstOrDefault();
                var empMatch = await _workFlow.Repository<Employee>().FindAsync(e => e.UserId == nuevaVenta.UserId || e.Id == nuevaVenta.UserId);
                var vendedorEmpleado = empMatch.FirstOrDefault();

                if (vendedorEmpleado == null)
                {
                    return (false, "El usuario actual no tiene un perfil de Empleado asociado. No se puede registrar el movimiento de stock.");
                }

                string nombreVendedor = $"{vendedorEmpleado.FirstName} {vendedorEmpleado.LastName}".Trim();

                string nombreCliente = string.IsNullOrWhiteSpace(nuevaVenta.CustomerName)
                    ? "Público General"
                    : nuevaVenta.CustomerName;

                foreach (var detalle in nuevaVenta.SaleDetails)
                {
                    var articulo = await _workFlow.Repository<Article>().GetByIdAsync(detalle.ArticleId);

                    if (articulo == null) return (false, $"Artículo {detalle.ArticleId} no existe.");
                    if (articulo.Stock < detalle.Quantity) return (false, $"Stock insuficiente para {articulo.Name}.");

                    articulo.Stock -= (decimal)detalle.Quantity;
                    articulo.ModificationDate = DateTime.Now;

                    var nuevoMovimiento = new Movement
                    {
                        ArticleId = articulo.Id,
                        EmployeeId = vendedorEmpleado.Id,
                        ActionId = 2, // Venta
                        MovementDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        Observation = nuevaVenta.Notes,
                        Amount = (double)detalle.Quantity,
                        SalePrice = (double)detalle.UnitPrice,
                        PaymentMethod = nuevaVenta.PaymentType.ToString(),
                        Recipient = nombreCliente
                    };
                    await _workFlow.Repository<Movement>().AddAsync(nuevoMovimiento);

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

                await _workFlow.Repository<Sale>().AddAsync(nuevaVenta);
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