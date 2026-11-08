using InventoryAPI.Models;
using InventoryAPI.Repositories;
using InventoryAPI.Repositories.IRepositories;
using InventoryAPI.Services.IServices;
using System.Text.Json;
using System.Net;
using System.Net.Http.Headers;
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

                // Consulta avanzada como opción principal. Si el plan no permite
                // /full, se mantiene disponible la consulta básica ya funcional.
                static string? Field(JsonElement obj, string key)
                {
                    if (!obj.TryGetProperty(key, out var val)) return null;
                    var value = val.ValueKind switch
                    {
                        JsonValueKind.String => val.GetString()?.Trim(),
                        JsonValueKind.Number => val.GetRawText(),
                        _ => null
                    };
                    return string.IsNullOrWhiteSpace(value) || value == "-" ? null : value;
                }

                async Task<HttpResponseMessage> RequestAsync(string path)
                {
                    using var req = new HttpRequestMessage(HttpMethod.Get,
                        $"https://api.decolecta.com/v1/sunat/{path}?numero={ruc}");
                    req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiToken.Trim());
                    req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    return await httpClient.SendAsync(req);
                }

                using var advancedResponse = await RequestAsync("ruc/full");
                var advancedAvailable = advancedResponse.IsSuccessStatusCode;
                HttpResponseMessage? basicResponse = null;
                HttpResponseMessage finalResponse = advancedResponse;
                try
                {
                    if (!advancedAvailable &&
                        advancedResponse.StatusCode is HttpStatusCode.BadRequest
                            or HttpStatusCode.Forbidden or HttpStatusCode.NotFound
                            or HttpStatusCode.UnprocessableEntity)
                    {
                        basicResponse = await RequestAsync("ruc");
                        finalResponse = basicResponse;
                    }

                    var json = await finalResponse.Content.ReadAsStringAsync();
                    if (!finalResponse.IsSuccessStatusCode)
                    {
                        var detail = "Consulta no autorizada o servicio no disponible.";
                        try
                        {
                            using var errorJson = JsonDocument.Parse(json);
                            if (errorJson.RootElement.ValueKind == JsonValueKind.Object)
                                detail = Field(errorJson.RootElement, "message") ??
                                    Field(errorJson.RootElement, "error") ?? detail;
                        }
                        catch (JsonException) { /* Error externo sin estructura JSON. */ }
                        return (false, null,
                            $"Decolecta rechazó la consulta ({(int)finalResponse.StatusCode}): {detail}");
                    }

                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    if (root.ValueKind != JsonValueKind.Object)
                        return (false, null, "La consulta SUNAT devolvió una respuesta inválida.");

                    var businessName = Field(root, "razon_social");
                    if (string.IsNullOrWhiteSpace(businessName))
                        return (false, null, "SUNAT no devolvió una razón social válida para ese RUC.");

                    // Los datos fiscales que ya existen en la BD se pueden guardar.
                    // Los nuevos campos avanzados son [NotMapped] y solo informativos.
                    var supplier = new Supplier
                    {
                        InventoryId = 0,
                        Ruc = ruc,
                        BusinessName = businessName,
                        Address = Field(root, "direccion"),
                        StatusId = 1,
                        Estado = Field(root, "estado"),
                        Condicion = Field(root, "condicion"),
                        Distrito = Field(root, "distrito"),
                        Provincia = Field(root, "provincia"),
                        Departamento = Field(root, "departamento"),
                        AdvancedDataAvailable = advancedAvailable,
                        Ubigeo = Field(root, "ubigeo"),
                        TipoContribuyente = Field(root, "tipo"),
                        ActividadEconomica = Field(root, "actividad_economica"),
                        NumeroTrabajadores = Field(root, "numero_trabajadores"),
                        TipoFacturacion = Field(root, "tipo_facturacion"),
                        TipoContabilidad = Field(root, "tipo_contabilidad"),
                        ComercioExterior = Field(root, "comercio_exterior"),
                        FiscalDataJson = advancedAvailable
                            ? JsonSerializer.Serialize(new Dictionary<string, object?>
                            {
                                ["tipo"] = Field(root, "tipo"),
                                ["actividad_economica"] = Field(root, "actividad_economica"),
                                ["ubigeo"] = Field(root, "ubigeo"),
                                ["numero_trabajadores"] = Field(root, "numero_trabajadores"),
                                ["tipo_facturacion"] = Field(root, "tipo_facturacion"),
                                ["tipo_contabilidad"] = Field(root, "tipo_contabilidad"),
                                ["comercio_exterior"] = Field(root, "comercio_exterior"),
                                ["via_tipo"] = Field(root, "via_tipo"),
                                ["via_nombre"] = Field(root, "via_nombre"),
                                ["zona_codigo"] = Field(root, "zona_codigo"),
                                ["zona_tipo"] = Field(root, "zona_tipo"),
                                ["numero"] = Field(root, "numero"),
                                ["interior"] = Field(root, "interior"),
                                ["lote"] = Field(root, "lote"),
                                ["dpto"] = Field(root, "dpto"),
                                ["manzana"] = Field(root, "manzana"),
                                ["kilometro"] = Field(root, "kilometro"),
                                ["es_agente_retencion"] = root.TryGetProperty("es_agente_retencion", out var agente) &&
                                    agente.ValueKind == JsonValueKind.True,
                                ["es_buen_contribuyente"] = root.TryGetProperty("es_buen_contribuyente", out var buen) &&
                                    buen.ValueKind == JsonValueKind.True
                            })
                            : null
                    };
                    return (true, supplier,
                        advancedAvailable ? "Consulta RUC avanzada completada." :
                            "Consulta RUC básica completada; los datos avanzados no están disponibles con esta credencial.");
                }
                finally
                {
                    basicResponse?.Dispose();
                }
            }
            catch (Exception ex)
            {
                return (false, null, $"Falla en el servidor interno: {ex.Message}");
            }
        }
    }
}