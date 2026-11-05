using ControlInventario.Shared.Models;
using InventoryAPI.Repositories;
using InventoryAPI.Repositories.IRepositories;
using InventoryAPI.Services.IServices;
using System.Text.Json;

namespace InventoryAPI.Services
{
    public class SupplierService(IWorkFlow workFlow, HttpClient httpClient) : WorkContainer<Supplier>(workFlow), ISupplierService
    {
        public async Task<(bool Success, Supplier? Data, string Message)> ConsultarRucAsync(string ruc)
        {
            if (string.IsNullOrWhiteSpace(ruc) || ruc.Length != 11)
            {
                return (false, null, "El RUC debe tener exactamente 11 dígitos numéricos.");
            }

            try
            {
                var localMatch = await _workFlow.Repository<Supplier>().FindAsync(s => s.Ruc == ruc);
                var proveedorExistente = localMatch.FirstOrDefault();

                if (proveedorExistente != null)
                {
                    return (true, proveedorExistente, "Encontrado en caché local.");
                }

                var parametroToken = await _workFlow.Repository<Parameters>()
                    .FindAsync(p => p.ParameterType == "API_KEY" && p.Name == "Decolecta_Token");

                string apiToken = parametroToken.FirstOrDefault()?.Description ?? "";

                if (string.IsNullOrWhiteSpace(apiToken))
                {
                    return (false, null, "El servicio de SUNAT está inactivo. El administrador debe configurar el Token desde la App.");
                }

                // 3. CONSULTA A DECOLECTA CON EL TOKEN DINÁMICO
                string url = $"https://api.decolecta.com/v1/sunat/ruc?numero={ruc}";

                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiToken);
                request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                var response = await httpClient.SendAsync(request);
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