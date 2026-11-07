using InventoryAPI.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace InventoryAPI.Controllers;

/// <summary>
/// Consulta de datos de RUC, solo para desarrolladores.
/// No lee ni crea proveedores de ninguna empresa.
/// Usa la integración externa Decolecta existente (datos de SUNAT).
/// </summary>
[ApiController]
[Authorize]
[Route("api/dev/companies")]
public sealed class DevCompanyRucController(
    AppDbContext db, IHttpClientFactory httpFactory, IConfiguration config) : ControllerBase
{
    private bool IsDeveloper() => User.FindFirst("RoleId")?.Value == "1" &&
        int.TryParse(User.FindFirst("UserId")?.Value, out var uid) && uid > 0;

    [HttpGet("lookup-ruc/{ruc}")]
    public async Task<IActionResult> LookupRuc(string ruc, CancellationToken cancellationToken)
    {
        if (!IsDeveloper()) return Forbid();
        if (!Regex.IsMatch(ruc, @"^\d{11}$"))
            return BadRequest(new { error = "El RUC debe tener exactamente 11 dígitos." });

        // Configuración secreta recomendada: Decolecta:Token / Decolecta__Token.
        // Compatibilidad con el token ya registrado en Parámetros de la empresa administradora.
        var token = config["Decolecta:Token"];
        if (string.IsNullOrWhiteSpace(token))
        {
            if (!int.TryParse(User.FindFirst("CompanyId")?.Value, out var adminCompanyId) || adminCompanyId <= 0)
                return Forbid();
            token = await db.Parameters.AsNoTracking()
                .Where(p => p.CompanyId == adminCompanyId &&
                    p.ParameterType == "API_KEY" && p.Name == "Decolecta_Token")
                .Select(p => p.Description).FirstOrDefaultAsync(cancellationToken);
        }
        if (string.IsNullOrWhiteSpace(token))
            return StatusCode(503, new { error = "Configura el token Decolecta en el servidor para consultar RUC." });

        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://api.decolecta.com/v1/sunat/ruc?numero={ruc}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        try
        {
            var client = httpFactory.CreateClient();
            using var response = await client.SendAsync(request,
                HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode)
                return StatusCode(502, new { error = "El servicio de consulta de RUC no respondió correctamente." });
            await using var body = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var document = await JsonDocument.ParseAsync(body, cancellationToken: timeout.Token);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return StatusCode(502, new { error = "La respuesta del servicio de RUC no es válida." });

            static string Get(JsonElement obj, string key) =>
                obj.TryGetProperty(key, out var p) && p.ValueKind == JsonValueKind.String
                    ? p.GetString()?.Trim() ?? "" : "";
            var name = Get(root, "razon_social");
            if (string.IsNullOrWhiteSpace(name))
                return NotFound(new { error = "No se encontró una razón social válida para este RUC." });

            return Ok(new
            {
                ruc,
                businessName = name,
                address = Get(root, "direccion"),
                estado = Get(root, "estado"),
                condicion = Get(root, "condicion"),
                source = "Decolecta / SUNAT"
            });
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return StatusCode(504, new { error = "La consulta de RUC superó el tiempo de espera." });
        }
        catch (HttpRequestException)
        {
            return StatusCode(502, new { error = "No se pudo conectar al servicio de consulta de RUC." });
        }
        catch (JsonException)
        {
            return StatusCode(502, new { error = "El servicio de RUC devolvió un formato no válido." });
        }
    }
}
