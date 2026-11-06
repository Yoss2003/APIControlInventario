using InventoryAPI.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryAPI.Controllers;

[Authorize]
[ApiController]
[Route("api/Articles")]
public sealed class ArticleMediaController(AppDbContext db, IWebHostEnvironment environment) : ControllerBase
{
    private bool IsDeveloper => User.FindFirst("RoleId")?.Value == "1";
    private readonly AppDbContext _db = db;
    private readonly IWebHostEnvironment _environment = environment;
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

        string folder = Path.Combine(_environment.ContentRootPath, "PrivateFiles", "articles", companyId.ToString(), id.ToString());
        Directory.CreateDirectory(folder);
        var written = new List<string>();
        var previousPhoto = article.MainPhotoPath;
        var previousVoucher = article.MainVoucherPath;
        try
        {
            foreach (var (file, kind) in new[] { (photo, "photo"), (voucher, "voucher") })
            {
                if (file is null) continue;
                var name = Guid.NewGuid().ToString("N") + Path.GetExtension(file.FileName).ToLowerInvariant();
                var full = Path.Combine(folder, name);
                await using (var output = new FileStream(full, FileMode.CreateNew))
                    await file.CopyToAsync(output);
                written.Add(full);
                var rel = Path.Combine(companyId.ToString(), id.ToString(), name).Replace('\\','/');
                if (kind == "photo") article.MainPhotoPath = rel;
                else article.MainVoucherPath = rel;
            }
            await _db.SaveChangesAsync();
        }
        catch
        {
            foreach (var file in written) if (System.IO.File.Exists(file)) System.IO.File.Delete(file);
            return StatusCode(500, new { error = "No se pudieron almacenar los archivos." });
        }
        foreach (var old in new[] { photo != null ? previousPhoto : null, voucher != null ? previousVoucher : null })
        {
            if (string.IsNullOrWhiteSpace(old)) continue;
            var prior = Path.Combine(_environment.ContentRootPath, "PrivateFiles", "articles", old.Replace('/', Path.DirectorySeparatorChar));
            if (Path.GetFullPath(prior).StartsWith(Path.GetFullPath(folder) + Path.DirectorySeparatorChar) && System.IO.File.Exists(prior))
                System.IO.File.Delete(prior);
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
        if (!await AccessibleInventoryIds(companyId, int.Parse(User.FindFirst("UserId")?.Value ?? "0"), true).ContainsAsync(article.InventoryId)) return Forbid();
        var relative = kind == "photo" ? article.MainPhotoPath : article.MainVoucherPath;
        if (kind == "photo") article.MainPhotoPath = null;
        else article.MainVoucherPath = null;
        await _db.SaveChangesAsync();
        if (!string.IsNullOrWhiteSpace(relative))
        {
            var root = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, "PrivateFiles", "articles"));
            var full = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (full.StartsWith(root + Path.DirectorySeparatorChar) && System.IO.File.Exists(full))
                System.IO.File.Delete(full);
        }
        return NoContent();
    }

    [HttpGet("{id:int}/media/{kind}")]
    public async Task<IActionResult> Download(int id, string kind)
    {
        if (!TryCompany(out var companyId)) return Unauthorized();
        var article = await _db.Articles.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id && a.CompanyId == companyId);
        if (article == null) return NotFound();
        if (!await AccessibleInventoryIds(companyId, int.Parse(User.FindFirst("UserId")?.Value ?? "0")).ContainsAsync(article.InventoryId)) return Forbid();
        var relative = kind == "photo" ? article.MainPhotoPath : kind == "voucher" ? article.MainVoucherPath : null;
        if (string.IsNullOrWhiteSpace(relative)) return NotFound();
        var root = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, "PrivateFiles", "articles"));
        var file = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!file.StartsWith(root + Path.DirectorySeparatorChar) || !System.IO.File.Exists(file)) return NotFound();
        var ext = Path.GetExtension(file).ToLowerInvariant();
        var type = ext switch { ".pdf" => "application/pdf", ".png" => "image/png", ".webp" => "image/webp", _ => "image/jpeg" };
        Response.Headers["Cache-Control"] = "private, no-store";
        return PhysicalFile(file, type, enableRangeProcessing: true);
    }
}
