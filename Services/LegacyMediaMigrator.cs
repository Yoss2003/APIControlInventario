using InventoryAPI.Data;
using InventoryAPI.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace InventoryAPI.Services;

/// <summary>
/// Migración explícita de medios anteriores almacenados en PrivateFiles.
/// No forma parte del arranque normal. No borra archivos físicos anteriores.
/// </summary>
public sealed class LegacyMediaMigrator(
    AppDbContext db,
    PrivateMediaStorage storage,
    IConfiguration config,
    IWebHostEnvironment environment,
    ILogger<LegacyMediaMigrator> logger)
{
    public async Task RunAsync(CancellationToken ct = default)
    {
        if (!storage.IsConfigured)
            throw new InvalidOperationException("Configura Supabase:Url, Supabase:SecretKey y Supabase:Bucket antes de migrar.");

        var legacyRoot = config["MediaMigration:LegacyRoot"];
        var contentRoot = Path.GetFullPath(string.IsNullOrWhiteSpace(legacyRoot)
            ? environment.ContentRootPath : legacyRoot);
        var copied = 0;
        var unavailable = 0;
        var errors = 0;

        var articles = await db.Articles.ToListAsync(ct);
        foreach (var article in articles)
        {
            if (!string.IsNullOrWhiteSpace(article.MainPhotoPath) &&
                !PrivateMediaStorage.IsCloudReference(article.MainPhotoPath))
                await MoveArticleField(article, "photo", article.MainPhotoPath, ct);
            if (!string.IsNullOrWhiteSpace(article.MainVoucherPath) &&
                !PrivateMediaStorage.IsCloudReference(article.MainVoucherPath))
                await MoveArticleField(article, "voucher", article.MainVoucherPath, ct);
        }

        var assignments = await db.ArticleAssignments.ToListAsync(ct);
        foreach (var assignment in assignments)
        {
            if (string.IsNullOrWhiteSpace(assignment.EvidencePath) ||
                PrivateMediaStorage.IsCloudReference(assignment.EvidencePath)) continue;

            var source = FindSource("assignments", assignment.CompanyId, assignment.Id, assignment.EvidencePath);
            if (source is null) { unavailable++; logger.LogWarning("Falta evidencia legacy de custodia {Id}", assignment.Id); continue; }

            var remote = await Transfer(source, assignment.CompanyId, "assignments", assignment.Id, "evidence", ct);
            if (remote is null) { errors++; continue; }
            assignment.EvidencePath = remote;
            try { await db.SaveChangesAsync(ct); copied++; }
            catch
            {
                assignment.EvidencePath = Path.GetRelativePath(Path.Combine(contentRoot, "PrivateFiles", "assignments"), source).Replace('\\', '/');
                await storage.DeleteAsync(remote, CancellationToken.None);
                throw;
            }
        }

        logger.LogInformation("Migración de medios terminada: {Copied} subidos; {Missing} sin archivo original; {Errors} no subidos.",
            copied, unavailable, errors);

        async Task MoveArticleField(Article article, string kind, string previous, CancellationToken cancellation)
        {
            var source = FindSource("articles", article.CompanyId, article.Id, previous);
            if (source is null) { unavailable++; logger.LogWarning("Falta {Kind} legacy del artículo {Id}", kind, article.Id); return; }
            var remote = await Transfer(source, article.CompanyId, "articles", article.Id, kind, cancellation);
            if (remote is null) { errors++; return; }

            if (kind == "photo") article.MainPhotoPath = remote;
            else article.MainVoucherPath = remote;
            try { await db.SaveChangesAsync(cancellation); copied++; }
            catch
            {
                if (kind == "photo") article.MainPhotoPath = previous;
                else article.MainVoucherPath = previous;
                await storage.DeleteAsync(remote, CancellationToken.None);
                throw;
            }
        }

        string? FindSource(string resource, int companyId, int recordId, string reference)
        {
            var root = Path.GetFullPath(Path.Combine(contentRoot, "PrivateFiles", resource));
            var expectedFolder = Path.GetFullPath(Path.Combine(root, companyId.ToString(), recordId.ToString()));
            var actual = Path.GetFullPath(Path.Combine(root, reference.Replace('/', Path.DirectorySeparatorChar)));
            if (!actual.StartsWith(expectedFolder + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
                !File.Exists(actual)) return null;
            return actual;
        }

        async Task<string?> Transfer(string source, int companyId, string resource, int recordId, string kind, CancellationToken cancellation)
        {
            var ext = Path.GetExtension(source).ToLowerInvariant();
            if (ext is not (".jpg" or ".jpeg" or ".png" or ".webp" or ".pdf") ||
                (kind != "voucher" && ext == ".pdf")) return null;
            var info = new FileInfo(source);
            var max = resource == "assignments" ? 8_000_000 : 10 * 1024 * 1024;
            if (info.Length <= 0 || info.Length > max) return null;
            var mime = ext switch { ".pdf" => "application/pdf", ".png" => "image/png",
                ".webp" => "image/webp", _ => "image/jpeg" };
            var key = PrivateMediaStorage.NewKey(companyId, resource, recordId, kind, ext);
            try
            {
                await using var stream = File.OpenRead(source);
                var formFile = new FormFile(stream, 0, stream.Length, "file", Path.GetFileName(source))
                {
                    Headers = new HeaderDictionary(),
                    ContentType = mime
                };
                return await storage.UploadAsync(key, formFile, mime, cancellation);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "No se pudo migrar {Resource}/{Id}/{Kind}", resource, recordId, kind);
                return null;
            }
        }
    }
}
