using ControlInventario.Shared.Models;
using InventoryAPI.Repositories;
using InventoryAPI.Repositories.IRepositories;
using InventoryAPI.Services.IServices;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

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
                // 1. CACHÉ LOCAL: Buscamos primero en la base de datos (con IWorkFlow)
                var localMatch = await _workFlow.Repository<Supplier>().FindAsync(s => s.Ruc == ruc);
                var proveedorExistente = localMatch.FirstOrDefault();

                if (proveedorExistente != null)
                {
                    return (true, proveedorExistente, "Encontrado en caché local.");
                }

                string apiToken = "sk_13723.G2u2hnJk9acgY3uFHZMYsJliHho0GXu4";

                // 2. INTENTO 1: Endpoint Avanzado (/full)
                Debug.WriteLine($"[INTENTO 1] Consultando RUC Avanzado para {ruc}...");
                string urlAvanzada = $"https://api.decolecta.com/v1/sunat/ruc/full?numero={ruc}";

                var request = new HttpRequestMessage(HttpMethod.Get, urlAvanzada);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiToken);

                var response = await httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    string jsonString = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(jsonString);
                    var root = doc.RootElement;

                    var nuevoProveedor = new Supplier
                    {
                        InventoryId = 0,
                        Ruc = ruc,
                        BusinessName = root.TryGetProperty("razon_social", out var rz) ? rz.GetString() ?? "SIN NOMBRE LEGAL" : "SIN NOMBRE LEGAL",
                        Address = root.TryGetProperty("direccion", out var dir) ? dir.GetString() ?? "Dirección no registrada" : "Dirección no registrada",
                        StatusId = 1,
                        Estado = root.TryGetProperty("estado", out var est) ? est.GetString() ?? "ACTIVO" : "ACTIVO",
                        Condicion = root.TryGetProperty("condicion", out var cond) ? cond.GetString() ?? "HABIDO" : "HABIDO",
                        Distrito = root.TryGetProperty("distrito", out var dist) ? dist.GetString() ?? "" : "",
                        Provincia = root.TryGetProperty("provincia", out var prov) ? prov.GetString() ?? "" : "",
                        Departamento = root.TryGetProperty("departamento", out var dep) ? dep.GetString() ?? "" : ""
                    };

                    await _workFlow.Repository<Supplier>().AddAsync(nuevoProveedor);
                    await _workFlow.CompleteAsync();
                    return (true, nuevoProveedor, "RUC localizado (Avanzado) y guardado en local.");
                }

                // 3. INTENTO 2 (FALLBACK): Endpoint Básico
                Debug.WriteLine($"[FALLBACK] Intentando endpoint básico para {ruc}...");
                string urlBasica = $"https://api.decolecta.com/v1/sunat/ruc?numero={ruc}";

                var requestBasico = new HttpRequestMessage(HttpMethod.Get, urlBasica);
                requestBasico.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiToken);

                var responseBasica = await httpClient.SendAsync(requestBasico);

                if (responseBasica.IsSuccessStatusCode)
                {
                    string jsonString = await responseBasica.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(jsonString);
                    var root = doc.RootElement;

                    string razonSocial = (root.TryGetProperty("razonSocial", out var rz1) ? rz1.GetString() : null)
                                      ?? (root.TryGetProperty("razon_social", out var rz2) ? rz2.GetString() : null)
                                      ?? "SIN NOMBRE LEGAL";

                    var nuevoProveedorBasico = new Supplier
                    {
                        InventoryId = 0,
                        Ruc = ruc,
                        BusinessName = razonSocial,
                        Address = root.TryGetProperty("direccion", out var dir1) ? dir1.GetString() ?? "Dirección no registrada" : "Dirección no registrada",
                        StatusId = 1,
                        Estado = root.TryGetProperty("estado", out var est1) ? est1.GetString() ?? "ACTIVO" : "ACTIVO",
                        Condicion = root.TryGetProperty("condicion", out var cond1) ? cond1.GetString() ?? "HABIDO" : "HABIDO",
                        Distrito = root.TryGetProperty("distrito", out var dist1) ? dist1.GetString() ?? "" : "",
                        Provincia = root.TryGetProperty("provincia", out var prov1) ? prov1.GetString() ?? "" : "",
                        Departamento = root.TryGetProperty("departamento", out var dep1) ? dep1.GetString() ?? "" : ""
                    };

                    await _workFlow.Repository<Supplier>().AddAsync(nuevoProveedorBasico);
                    await _workFlow.CompleteAsync();
                    return (true, nuevoProveedorBasico, "RUC localizado (Básico) y guardado en local.");
                }

                return (false, null, $"El RUC {ruc} no fue localizado en SUNAT (Fallo en consulta avanzada y básica).");
            }
            catch (Exception ex)
            {
                return (false, null, $"Falla crítica en el ecosistema en cascada de RUC: {ex.Message}");
            }
        }
    }
}