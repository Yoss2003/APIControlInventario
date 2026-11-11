using InventoryAPI.Data;
using InventoryAPI.Models;
using InventoryAPI.Repositories;
using InventoryAPI.Repositories.IRepositories;
using InventoryAPI.Services.IServices;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace InventoryAPI.Services
{
    // DNI y RUC utilizan la misma configuración central de Decolecta.
    // Nunca se devuelve el token o la respuesta cruda de errores del proveedor.
    public sealed class CustomerService(
        IWorkFlow workFlow, HttpClient httpClient, AppDbContext db, IConfiguration configuration)
        : WorkContainer<Customer>(workFlow), ICustomerService
    {
        public async Task<(bool IsSuccess, string DataOrError)> ConsultarDniExternoAsync(string dni)
        {
            if (!Regex.IsMatch(dni ?? string.Empty, @"^[0-9]{8}$"))
                return (false, "El DNI debe tener exactamente 8 dígitos numéricos.");

            // La misma prioridad y el mismo ámbito de administración central
            // que se utilizan en SupplierService para consultar RUC.
            var token = configuration["Decolecta:Token"]?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(token))
            {
                token = await db.Parameters.AsNoTracking()
                    .Where(p => p.CompanyId == 1 &&
                        p.ParameterType == "API_KEY" && p.Name == "Decolecta_Token")
                    .OrderBy(p => p.InventoryId == 0 ? 0 : 1)
                    .Select(p => p.Description)
                    .FirstOrDefaultAsync() ?? string.Empty;
                token = token.Trim();
            }

            if (string.IsNullOrWhiteSpace(token))
                return (false, "El servicio de DNI no está configurado. Configura el token maestro de Decolecta.");

            try
            {
                using var request = new HttpRequestMessage(
                    HttpMethod.Get,
                    $"https://api.decolecta.com/v1/reniec/dni?numero={dni}");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                using var response = await httpClient.SendAsync(
                    request, HttpCompletionOption.ResponseHeadersRead);

                if (!response.IsSuccessStatusCode)
                {
                    var message = response.StatusCode switch
                    {
                        HttpStatusCode.Unauthorized =>
                            "Decolecta rechazó la clave de acceso para consultar DNI (401).",
                        HttpStatusCode.Forbidden =>
                            "La clave no tiene acceso a consultas DNI (403). Revisa el plan RENIEC en Decolecta.",
                        HttpStatusCode.NotFound =>
                            "No se encontraron datos para el DNI consultado (404).",
                        HttpStatusCode.TooManyRequests =>
                            "Se alcanzó el límite de consultas DNI de Decolecta (429).",
                        _ =>
                            $"Decolecta no pudo responder la consulta DNI (HTTP {(int)response.StatusCode})."
                    };
                    return (false, message);
                }

                using var document = JsonDocument.Parse(
                    await response.Content.ReadAsStringAsync());
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                    return (false, "La consulta DNI devolvió un formato inesperado.");

                // Compatibilidad con respuestas envueltas y respuestas planas.
                if (root.TryGetProperty("data", out var data) &&
                    data.ValueKind == JsonValueKind.Object)
                    root = data;

                static string Field(JsonElement obj, string name)
                {
                    return obj.TryGetProperty(name, out var prop) &&
                        prop.ValueKind == JsonValueKind.String
                        ? prop.GetString()?.Trim() ?? string.Empty : string.Empty;
                }

                var firstName = Field(root, "first_name");
                var firstLastName = Field(root, "first_last_name");
                var secondLastName = Field(root, "second_last_name");
                var returnedDocument = Field(root, "document_number");
                if (!string.IsNullOrWhiteSpace(returnedDocument) &&
                    !string.Equals(dni, returnedDocument, StringComparison.Ordinal))
                    return (false, "El servicio devolvió datos de un DNI diferente. No se usarán.");
                if (string.IsNullOrWhiteSpace(firstName))
                    return (false, "No se obtuvo un nombre válido para el DNI consultado.");

                // Sólo proyectar los campos necesarios para autocompletar el formulario.
                var result = JsonSerializer.Serialize(new
                {
                    first_name = firstName,
                    first_last_name = firstLastName,
                    second_last_name = secondLastName,
                    full_name = string.Join(" ", new[]
                        { firstName, firstLastName, secondLastName }
                        .Where(value => !string.IsNullOrWhiteSpace(value))),
                    document_number = dni
                });
                return (true, result);
            }
            catch (JsonException)
            {
                return (false, "La consulta DNI devolvió datos que no se pudieron interpretar.");
            }
            catch (TaskCanceledException)
            {
                return (false, "La consulta DNI agotó el tiempo de respuesta. Intenta nuevamente.");
            }
            catch (HttpRequestException)
            {
                return (false, "No se pudo conectar con Decolecta para consultar DNI.");
            }
        }
    }
}
