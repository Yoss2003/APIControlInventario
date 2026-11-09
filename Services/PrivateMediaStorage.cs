using System.Net.Http.Headers;
using System.Text.RegularExpressions;

namespace InventoryAPI.Services;

/// <summary>
/// Contenedor privado de archivos persistentes. Solo InventoryAPI posee la clave
/// administrativa; los clientes se autentican en nuestros controladores habituales.
/// La base de datos conserva exclusivamente una referencia con prefijo "sb:".
/// </summary>
public sealed class PrivateMediaStorage(HttpClient client, IConfiguration config)
{
    public const string ReferencePrefix = "sb:";
    private readonly HttpClient _client = client;
    private readonly IConfiguration _config = config;

    private static readonly Regex ValidObjectKey = new(
        @"^companies/[1-9]\d*/(articles|assignments)/[1-9]\d*/(photo|voucher|evidence)/[a-f0-9]{32}\.(jpg|jpeg|png|webp|pdf)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public bool IsConfigured =>
        Uri.TryCreate(_config["Supabase:Url"], UriKind.Absolute, out var url) &&
        url.Scheme == Uri.UriSchemeHttps &&
        !string.IsNullOrWhiteSpace(_config["Supabase:SecretKey"]) &&
        !string.IsNullOrWhiteSpace(_config["Supabase:Bucket"]);

    public static bool IsCloudReference(string? reference) =>
        reference?.StartsWith(ReferencePrefix, StringComparison.Ordinal) == true;

    public static string NewKey(int companyId, string resource, int resourceId, string kind, string extension)
    {
        if (companyId <= 0 || resourceId <= 0 || resource is not ("articles" or "assignments") ||
            kind is not ("photo" or "voucher" or "evidence") ||
            extension is not (".jpg" or ".jpeg" or ".png" or ".webp" or ".pdf"))
            throw new ArgumentException("Referencia de archivo inválida.");
        return $"companies/{companyId}/{resource}/{resourceId}/{kind}/{Guid.NewGuid():N}{extension}";
    }

    private string ExtractKey(string reference)
    {
        var key = reference.StartsWith(ReferencePrefix, StringComparison.Ordinal)
            ? reference[ReferencePrefix.Length..] : reference;
        if (!ValidObjectKey.IsMatch(key)) throw new ArgumentException("Referencia de archivo inválida.");
        return key;
    }

    private Uri Endpoint(string path, bool authenticatedDownload = false)
    {
        if (!IsConfigured) throw new InvalidOperationException("Supabase Storage privado no está configurado en InventoryAPI.");
        var baseUrl = _config["Supabase:Url"]!.TrimEnd('/');
        var bucket = Uri.EscapeDataString(_config["Supabase:Bucket"]!);
        var encodedPath = string.Join("/", path.Split('/').Select(Uri.EscapeDataString));
        var route = authenticatedDownload ? "object/authenticated" : "object";
        return new Uri($"{baseUrl}/storage/v1/{route}/{bucket}/{encodedPath}");
    }

    private HttpRequestMessage Request(HttpMethod method, string key, bool download = false)
    {
        var request = new HttpRequestMessage(method, Endpoint(ExtractKey(key), download));
        var secret = _config["Supabase:SecretKey"]!.Trim();
        request.Headers.TryAddWithoutValidation("apikey", secret);
        // Las claves modernas sb_secret_* no son JWT: exclusivamente apikey.
        // La clave legacy service_role sí admite Authorization: Bearer.
        if (!secret.StartsWith("sb_secret_", StringComparison.Ordinal))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret);
        return request;
    }

    public async Task<string> UploadAsync(string key, IFormFile file, string contentType, CancellationToken ct)
    {
        using var request = Request(HttpMethod.Post, key);
        request.Headers.TryAddWithoutValidation("x-upsert", "false");
        await using var input = file.OpenReadStream();
        using var body = new StreamContent(input);
        body.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        request.Content = body;
        using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
        {
            // Storage devuelve un codigo y un mensaje precisos en el cuerpo JSON.
            // No registrar cabeceras, URL completas ni credenciales del servidor.
            string errorCode = "UnknownStorageError";
            string errorMessage = "Sin detalle devuelto por Supabase.";
            try
            {
                var raw = await response.Content.ReadAsStringAsync(ct);
                using var document = System.Text.Json.JsonDocument.Parse(raw);
                if (document.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    foreach (var prop in document.RootElement.EnumerateObject())
                    {
                        if (prop.Value.ValueKind != System.Text.Json.JsonValueKind.String) continue;
                        if (prop.NameEquals("code") || prop.NameEquals("error"))
                            errorCode = prop.Value.GetString() ?? errorCode;
                        else if (prop.NameEquals("message"))
                            errorMessage = prop.Value.GetString() ?? errorMessage;
                    }
                }
            }
            catch (System.Text.Json.JsonException)
            {
                errorMessage = "Respuesta no JSON de Supabase; revisar estado HTTP.";
            }
            string Clean(string value)
            {
                var cleaned = Regex.Replace(value, @"sb_(?:secret|publishable)_[A-Za-z0-9_-]+", "[CLAVE OCULTA]");
                cleaned = Regex.Replace(cleaned, @"[\x00-\x1F\x7F]", " ");
                return cleaned.Length > 400 ? cleaned[..400] : cleaned;
            }
            throw new HttpRequestException(
                $"Supabase Storage rechazo la carga (HTTP {(int)response.StatusCode}, codigo: {Clean(errorCode)}, detalle: {Clean(errorMessage)}).");
        }
        return ReferencePrefix + ExtractKey(key);
    }

    public async Task<(byte[] Content, string ContentType)?> DownloadAsync(string reference, CancellationToken ct)
    {
        using var request = Request(HttpMethod.Get, reference, download: true);
        using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"No se pudo descargar el archivo privado (HTTP {(int)response.StatusCode}).");
        // Solo se almacenan archivos validados de hasta 10 MB desde los controladores.
        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        if (bytes.Length > 11 * 1024 * 1024)
            throw new InvalidDataException("Archivo remoto demasiado grande.");
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
        return (bytes, contentType);
    }

    public async Task DeleteAsync(string reference, CancellationToken ct)
    {
        using var request = Request(HttpMethod.Delete, reference);
        using var response = await _client.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NotFound)
            throw new HttpRequestException($"No se pudo eliminar el objeto privado (HTTP {(int)response.StatusCode}).");
    }
}
