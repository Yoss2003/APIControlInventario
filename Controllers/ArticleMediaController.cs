using InventoryAPI.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InventoryAPI.Services;

namespace InventoryAPI.Controllers;

[Authorize]
[ApiController]
[Route("api/Articles")]
public sealed class ArticleMediaController(AppDbContext db, IWebHostEnvironment environment, PrivateMediaStorage storage, ILogger<ArticleMediaController> logger) : ControllerBase
{
    private bool IsDeveloper => User.FindFirst("RoleId")?.Value == "1";
    private readonly AppDbContext _db = db;
    private readonly IWebHostEnvironment _environment = environment;
    private readonly PrivateMediaStorage _storage = storage;
    private readonly ILogger<ArticleMediaController> _logger = logger;
    private bool TryCompany(out int companyId) =>
        int.TryParse(User.FindFirst("CompanyId")?.Value, out companyId) && companyId > 0;

    private IQueryable<int> AccessibleInventoryIds(int cid, int uid, bool edit = false) =>
        _db.Inventories.Where(i => i.CompanyId == cid &&
            (IsDeveloper || i.UserId == uid || _db.SharedInventories.Any(sh => sh.InventoryId == i.Id &&
                sh.CompanyId == cid && sh.UserId == uid &&
                (!edit || sh.AccessLevel == InventoryAPI.Models.SharedInventory.AccessMode.Editor)))).Select(i => i.Id);

    private static async Task<bool> ValidSignature(IFormFile file, bool allowPdf)
    {
        if (file.Length == 0 || file.Length > 10 * 1024 * 1024) return false;
        await using var s = file.OpenReadStream();
        var magic = new byte[12];
        var read = await s.ReadAsync(magic);
        bool jpg = read >= 3 && magic[0] == 0xFF && magic[1] == 0xD8 && magic[2] == 0xFF;
        bool png = read >= 8 && magic.AsSpan(0, 8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10});
        bool webp = read >= 12 && System.Text.Encoding.ASCII.GetString(magic, 0, 4) == "RIFF" &&
            System.Text.Encoding.ASCII.GetString(magic, 8, 4) == "WEBP";
        bool pdf = read >= 5 && System.Text.Encoding.ASCII.GetString(magic, 0, 5) == "%PDF-";
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        return (jpg && (ext is ".jpg" or ".jpeg")) || (png && ext == ".png") ||
            (webp && ext == ".webp") || (allowPdf && pdf && ext == ".pdf");
    }

    [HttpPost("{id:int}/media")]
    [RequestSizeLimit(22 * 1024 * 1024)]
    public async Task<IActionResult> Upload(int id, IFormFile? photo, IFormFile? voucher)
    {
        if (!TryCompany(out var companyId)) return Unauthorized();
        var article = await _db.Articles.FirstOrDefaultAsync(a => a.Id == id && a.CompanyId == companyId);
        if (article is null) return NotFound();
        if (!await AccessibleInventoryIds(companyId, int.Parse(User.FindFirst("UserId")?.Value ?? "0"), true).ContainsAsync(article.InventoryId)) return Forbid();
        if (photo is null && voucher is null) return BadRequest(new { error = "Selecciona al menos un archivo." });
        if (photo is not null && !await ValidSignature(photo, false))
            return BadRequest(new { error = "Fotografía inválida. Usa JPG, PNG o WEBP (máx. 10 MB)." });
        if (voucher is not null && !await ValidSignature(voucher, true))
            return BadRequest(new { error = "Comprobante inválido. Usa imagen o PDF (máx. 10 MB)." });

        if (!_storage.IsConfigured)
            return StatusCode(503, new { error = "Almacenamiento privado no configurado en la API." });

        var written = new List<string>();
        var oldPhoto = article.MainPhotoPath;
        var oldVoucher = article.MainVoucherPath;
        try
        {
            foreach (var (file, kind) in new[] { (photo, "photo"), (voucher, "voucher") })
            {
                if (file is null) continue;
                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                var objectKey = PrivateMediaStorage.NewKey(companyId, "articles", id, kind, ext);
                var stored = await _storage.UploadAsync(objectKey, file, MediaType(ext), HttpContext.RequestAborted);
                written.Add(stored);
                if (kind == "photo") article.MainPhotoPath = stored;
                else article.MainVoucherPath = stored;
            }
            await _db.SaveChangesAsync(HttpContext.RequestAborted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo registrar el archivo privado del artículo {ArticleId}", id);
            foreach (var uploaded in written)
            {
                try { await _storage.DeleteAsync(uploaded, CancellationToken.None); }
                catch (Exception cleanupError) { _logger.LogWarning(cleanupError, "Objeto huérfano pendiente de limpiar"); }
            }
            return StatusCode(502, new { error = "No se pudo guardar el archivo en almacenamiento privado." });
        }

        // No borrar archivos legacy de disco hasta migrarlos. Los nuevos objetos
        // reemplazados pueden eliminarse después de guardar la nueva referencia.
        foreach (var old in new[] { photo != null ? oldPhoto : null, voucher != null ? oldVoucher : null })
            if (PrivateMediaStorage.IsCloudReference(old))
            {
                try { await _storage.DeleteAsync(old!, CancellationToken.None); }
                catch (Exception ex) { _logger.LogWarning(ex, "No se pudo limpiar un objeto anterior del artículo {ArticleId}", id); }
            }
        return Ok(new { article.MainPhotoPath, article.MainVoucherPath });
    }

    [HttpDelete("{id:int}/media/{kind}")]
    public async Task<IActionResult> Delete(int id, string kind)
    {
        if (!TryCompany(out var companyId)) return Unauthorized();
        if (kind is not ("photo" or "voucher")) return BadRequest(new { error = "Tipo desconocido." });
        var article = await _db.Articles.FirstOrDefaultAsync(a => a.Id == id && a.CompanyId == companyId);
        if (article is null) return NotFound();
        if (!await AccessibleInventoryIds(companyId, int.Parse(User.FindFirst("UserId")?.Value ?? "0"), true)
            .ContainsAsync(article.InventoryId)) return Forbid();
        var previous = kind == "photo" ? article.MainPhotoPath : article.MainVoucherPath;
        if (PrivateMediaStorage.IsCloudReference(previous) && !_storage.IsConfigured)
            return StatusCode(503, new { error = "Almacenamiento privado no configurado en la API." });
        if (kind == "photo") article.MainPhotoPath = null;
        else article.MainVoucherPath = null;
        await _db.SaveChangesAsync(HttpContext.RequestAborted);
        if (PrivateMediaStorage.IsCloudReference(previous))
        {
            try { await _storage.DeleteAsync(previous!, CancellationToken.None); }
            catch (Exception ex) { _logger.LogWarning(ex, "Objeto privado pendiente de limpieza tras borrar medio {ArticleId}", id); }
        }
        else if (!string.IsNullOrWhiteSpace(previous))
        {
            var root = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, "PrivateFiles", "articles"));
            var file = Path.GetFullPath(Path.Combine(root, previous.Replace('/', Path.DirectorySeparatorChar)));
            if (file.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal) && System.IO.File.Exists(file))
                System.IO.File.Delete(file);
        }
        return NoContent();
    }

    [HttpGet("{id:int}/media/{kind}")]
    public async Task<IActionResult> Download(int id, string kind)
    {
        if (!TryCompany(out var companyId)) return Unauthorized();
        if (kind is not ("photo" or "voucher")) return BadRequest(new { error = "Tipo desconocido." });
        var article = await _db.Articles.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id && a.CompanyId == companyId);
        if (article is null) return NotFound();
        if (!await AccessibleInventoryIds(companyId, int.Parse(User.FindFirst("UserId")?.Value ?? "0"))
            .ContainsAsync(article.InventoryId)) return Forbid();
        var reference = kind == "photo" ? article.MainPhotoPath : article.MainVoucherPath;
        if (string.IsNullOrWhiteSpace(reference)) return NotFound();

        Response.Headers.CacheControl = "private, no-store";
        if (PrivateMediaStorage.IsCloudReference(reference))
        {
            if (!_storage.IsConfigured)
                return StatusCode(503, new { error = "Almacenamiento privado no configurado en la API." });
            try
            {
                var remote = await _storage.DownloadAsync(reference, HttpContext.RequestAborted);
                if (remote is null) return NotFound();
                return File(remote.Value.Content, MediaType(Path.GetExtension(reference)), enableRangeProcessing: true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error consultando medio privado del artículo {ArticleId}", id);
                return StatusCode(502, new { error = "No se pudo recuperar el archivo del almacenamiento privado." });
            }
        }

        // Compatibilidad: no romper registros que todavía apuntan a disco.
        var root = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, "PrivateFiles", "articles"));
        var path = Path.GetFullPath(Path.Combine(root, reference.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
            !System.IO.File.Exists(path)) return NotFound();
        return PhysicalFile(path, MediaType(Path.GetExtension(path)), enableRangeProcessing: true);
    }

    private static string MediaType(string pathOrExtension)
    {
        var ext = pathOrExtension.StartsWith('.') ? pathOrExtension.ToLowerInvariant() :
            Path.GetExtension(pathOrExtension).ToLowerInvariant();
        return ext switch { ".pdf" => "application/pdf", ".png" => "image/png",
            ".webp" => "image/webp", _ => "image/jpeg" };
    }
}
