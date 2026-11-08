using InventoryAPI.Models;
using InventoryAPI.Repositories;
using InventoryAPI.Repositories.IRepositories;
using InventoryAPI.Services.IServices;
using System.Text.Json;
using InventoryAPI.Data;
using Microsoft.EntityFrameworkCore;

namespace InventoryAPI.Services
{
    public class SupplierService(IWorkFlow workFlow, HttpClient httpClient, AppDbContext db, IConfiguration configuration) : WorkContainer<Supplier>(workFlow), ISupplierService
    {
        public async Task<(bool Success, Supplier? Data, string Message)> ConsultarRucAsync(string ruc)
        {
            if (string.IsNullOrWhiteSpace(ruc) || ruc.Length != 11)
            {
                return (false, null, "El RUC debe tener exactamente 11 dígitos numéricos.");
            }

            try
            {
                // La caché se consulta desde el controller con CompanyId del JWT.
                // No reutilizar proveedores de otras empresas por coincidencia de RUC.

                // El token de consulta SUNAT es central. Nunca tomar el primero
                // de todas las empresas ni mostrarlo a un usuario.
                string apiToken = configuration["Decolecta:Token"]?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(apiToken))
                {
                    apiToken = await db.Parameters.AsNoTracking()
                        .Where(p => p.CompanyId == 1 && p.ParameterType == "API_KEY" &&
                            p.Name == "Decolecta_Token")
                        .OrderBy(p => p.InventoryId == 0 ? 0 : 1)
                        .Select(p => p.Description)
                        .FirstOrDefaultAsync() ?? "";
                }

                if (string.IsNullOrWhiteSpace(apiToken))
                {
                    return (false, null, "El servicio de SUNAT está inactivo. El administrador debe configurar el Token desde la App.");
                }

                // 3. CONSULTA A DECOLECTA CON EL TOKEN DINÁMICO
                string url = $"https://api.decolecta.com/v1/sunat/ruc?numero={ruc}";

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiToken);
                request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                using var response = await httpClient.SendAsync(request);
                string jsonString = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(jsonString);
                    var root = doc.RootElement;

                    var nuevoProveedor = new Supplier
                    {
                        InventoryId = 0,
                        Ruc = ruc,
                        BusinessName = root.TryGetProperty("razon_social", out var rz) ? rz.GetString() ?? "SIN NOMBRE" : "SIN NOMBRE",
                        Address = root.TryGetProperty("direccion", out var dir) ? dir.GetString() ?? "Sin dirección" : "Sin dirección",
                        StatusId = 1,
                        Estado = root.TryGetProperty("estado", out var est) ? est.GetString() ?? "ACTIVO" : "ACTIVO",
                        Condicion = root.TryGetProperty("condicion", out var cond) ? cond.GetString() ?? "HABIDO" : "HABIDO",
                        Distrito = root.TryGetProperty("distrito", out var dist) ? dist.GetString() ?? "" : "",
                        Provincia = root.TryGetProperty("provincia", out var prov) ? prov.GetString() ?? "" : "",
                        Departamento = root.TryGetProperty("departamento", out var dep) ? dep.GetString() ?? "" : ""
                    };

                    return (true, nuevoProveedor, "RUC localizado y guardado.");
                }
                else
                {
                    string errorDetalle = "Desconocido";
                    try
                    {
                        using var errDoc = JsonDocument.Parse(jsonString);
                        if (errDoc.RootElement.TryGetProperty("message", out var msg))
                        {
                            errorDetalle = msg.GetString() ?? "Sin mensaje";
                        }
                    }
                    catch { errorDetalle = "Error de conexión."; }

                    return (false, null, $"Decolecta rechazó la consulta ({(int)response.StatusCode}): {errorDetalle}");
                }
            }
            catch (Exception ex)
            {
                return (false, null, $"Falla en el servidor interno: {ex.Message}");
            }
        }
    }
}