using ControlInventario.Shared.Models;
using InventoryAPI.Repositories;
using InventoryAPI.Repositories.IRepositories;
using InventoryAPI.Services.IServices;

namespace InventoryAPI.Services
{
    public class ArticleService(IWorkFlow workFlow) : WorkContainer<Article>(workFlow), IArticleService
    {
        public override async Task<bool> CreateAsync(Article article)
        {
            await _workFlow.BeginTransactionAsync();

            try
            {
                await _workFlow.Repository<Article>().AddAsync(article);
                await _workFlow.CompleteAsync();

                var nuevoMovimiento = new Movement
                {
                    ArticleId = article.Id,
                    EmployeeId = article.LoggedUserId,
                    ActionId = 1,
                    MovementDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    Observation = "Registro inicial del producto en almacén",
                    Amount = article.Stock,
                    SalePrice = article.SalePrice ?? 0m,
                    CompanyId = article.CompanyId
                };
                await _workFlow.Repository<Movement>().AddAsync(nuevoMovimiento);

                string nombreUsuario = string.IsNullOrWhiteSpace(article.LoggedUserFullName)
                                       ? "Usuario Desconocido"
                                       : article.LoggedUserFullName;

                var nuevoLog = new HistoryLog
                {
                    LogDate = DateTime.Now,
                    Username = nombreUsuario,
                    ModuleName = "Inventario",
                    ActionName = "Creación",
                    Detail = $"Producto \"{article.Name}\" agregado por \"{nombreUsuario}\" el \"{DateTime.Now:dd/MM/yyyy HH:mm}\"",
                    CompanyId = article.CompanyId
                };
                await _workFlow.Repository<HistoryLog>().AddAsync(nuevoLog);

                await _workFlow.CompleteAsync();
                await _workFlow.CommitTransactionAsync();

                return true;
            }
            catch (Exception)
            {
                await _workFlow.RollbackTransactionAsync();
                throw;
            }
        }

        public async Task<int> GetArticleCountByInventoryIdAsync(int inventoryId)
        {
            var articles = await _workFlow.Repository<Article>().FindAsync(a => a.InventoryId == inventoryId);
            return (int)articles.Sum(a => a.Stock);
        }

        public async Task<Article?> GetArticleByBarcodeAsync(string barcode)
        {
            var articles = await _workFlow.Repository<Article>().FindAsync(a => a.Barcode == barcode);
            return articles.FirstOrDefault();
        }

        public async Task<(bool Success, string ErrorMessage)> AddDetailAsync(ArticleDetails detail)
        {
            await _workFlow.BeginTransactionAsync();
            try
            {
                await _workFlow.Repository<ArticleDetails>().AddAsync(detail);

                var padre = await _workFlow.Repository<Article>().GetByIdAsync(detail.ArticleId);
                if (padre != null)
                {
                    if (padre.Tracking.ToString() == "Serialized")
                    {
                        padre.Stock += 1;
                        _workFlow.Repository<Article>().Update(padre);
                    }
                }

                await _workFlow.CompleteAsync();
                await _workFlow.CommitTransactionAsync();

                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                await _workFlow.RollbackTransactionAsync();
                string msg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                Console.WriteLine($"[SERVICE_ERROR] AddDetailAsync: {msg}");
                return (false, msg);
            }
        }

        public async Task<bool> UpdateDetailAsync(ArticleDetails detail)
        {
            try
            {
                var existing = await _workFlow.Repository<ArticleDetails>().GetByIdAsync(detail.Id);
                if (existing == null) return false;
                existing.SerialNumber = detail.SerialNumber;
                _workFlow.Repository<ArticleDetails>().Update(existing);
                await _workFlow.CompleteAsync();
                return true;
            }
            catch { return false; }
        }

        public async Task<bool> DeleteDetailAsync(int id)
        {
            await _workFlow.BeginTransactionAsync();
            try
            {
                var existing = await _workFlow.Repository<ArticleDetails>().GetByIdAsync(id);
                if (existing == null) return false;

                existing.IsActive = false;
                _workFlow.Repository<ArticleDetails>().Update(existing);

                var padre = await _workFlow.Repository<Article>().GetByIdAsync(existing.ArticleId);
                if (padre != null && padre.Stock > 0)
                {
                    padre.Stock -= 1;
                    _workFlow.Repository<Article>().Update(padre);
                }

                await _workFlow.CompleteAsync();
                await _workFlow.CommitTransactionAsync();
                return true;
            }
            catch
            {
                await _workFlow.RollbackTransactionAsync();
                return false;
            }
        }

        public async Task<bool> UpdateArticleWithAuditAsync(int id, Article updatedArticle, int companyId)
        {
            await _workFlow.BeginTransactionAsync();
            try
            {
                // 1. Obtenemos el artículo original INTACTO desde la BD
                var existingArticle = await _workFlow.Repository<Article>().GetByIdAsync(id);
                if (existingArticle == null || existingArticle.CompanyId != companyId)
                {
                    await _workFlow.RollbackTransactionAsync();
                    return false;
                }

                // 2. Calculamos la diferencia ANTES de que se sobrescriban los datos
                decimal stockOriginal = existingArticle.Stock;
                decimal stockNuevo = updatedArticle.Stock;
                decimal diferenciaStock = stockNuevo - stockOriginal;

                // 3. Aplicamos el Reflection que tenías en el controlador
                foreach (var property in typeof(Article).GetProperties())
                {
                    if (property.Name != "Id" &&
                        property.Name != "CompanyId" &&
                        property.Name != "RegistrationDate" &&
                        property.Name != "IsActive" &&
                        property.Name != "IsSynced" &&
                        property.CanWrite)
                    {
                        var newValue = property.GetValue(updatedArticle);
                        property.SetValue(existingArticle, newValue);
                    }
                }

                _workFlow.Repository<Article>().Update(existingArticle);
                await _workFlow.CompleteAsync(); // Guardamos el cambio del artículo principal

                // 4. GENERACIÓN AUTOMÁTICA DEL KÁRDEX (Solo si varió el stock)
                string nombreEmpleado = string.IsNullOrWhiteSpace(updatedArticle.LoggedUserFullName)
                                        ? "Usuario Sistema" : updatedArticle.LoggedUserFullName;
                int empleadoId = updatedArticle.CurrentEmployeeId ?? 1;

                if (diferenciaStock != 0)
                {
                    var movimientoKardex = new Movement
                    {
                        ArticleId = existingArticle.Id,
                        EmployeeId = empleadoId,
                        ActionId = diferenciaStock > 0 ? 1 : 2, // 1 = Ingreso, 2 = Salida/Merma
                        MovementDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        Observation = $"Ajuste de stock automatizado ({(diferenciaStock > 0 ? "+" : "")}{diferenciaStock:0.##})",
                        Amount = Math.Abs(diferenciaStock),
                        SalePrice = 0,
                        PaymentMethod = "N/A",
                        Recipient = "Almacén Local",
                        CompanyId = companyId
                    };
                    await _workFlow.Repository<Movement>().AddAsync(movimientoKardex);
                }

                // 5. GENERACIÓN AUTOMÁTICA DEL HISTORIAL (HistoryLogs)
                var nuevoLog = new HistoryLog
                {
                    LogDate = DateTime.Now,
                    Username = nombreEmpleado,
                    ModuleName = "Inventario",
                    ActionName = "Modificación",
                    Detail = $"Producto \"{existingArticle.Name}\" modificado. Variación de stock: {diferenciaStock:0.##}.",
                    CompanyId = companyId
                };
                await _workFlow.Repository<HistoryLog>().AddAsync(nuevoLog);

                // 6. Confirmamos la transacción completa
                await _workFlow.CompleteAsync();
                await _workFlow.CommitTransactionAsync();

                return true;
            }
            catch (Exception ex)
            {
                await _workFlow.RollbackTransactionAsync();
                System.Diagnostics.Debug.WriteLine($"[AUTO-AUDIT ERROR]: {ex.Message}");
                return false;
            }
        }
    }
}