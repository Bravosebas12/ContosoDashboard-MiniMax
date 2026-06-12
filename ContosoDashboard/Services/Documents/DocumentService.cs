using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ContosoDashboard.Data;
using ContosoDashboard.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ContosoDashboard.Services.Documents;

/// <summary>
/// Implementación de <see cref="IDocumentService"/>.
/// Flujo de upload: validar → MIME magic bytes → AV scan → generar path
/// → persistir en disco → guardar DB (rollback on fail) → log.
/// Reglas de autorización: <see cref="UserHasAccessAsync"/> verifica ownership +
/// membresía de proyecto + shares activos.
/// </summary>
public class DocumentService : IDocumentService
{
    private readonly ApplicationDbContext _db;
    private readonly IFileStorageService _storage;
    private readonly IAntivirusScanner _av;
    private readonly IMimeTypeValidator _mimeValidator;
    private readonly IFilePathBuilder _pathBuilder;
    private readonly IActivityLogService _activityLog;
    private readonly INotificationService _notifications;
    private readonly ILogger<DocumentService> _logger;

    /// <summary>Conversión entre enums paralelos (mismo orden numérico).</summary>
    private static DocumentAvStatus ToAvStatus(ScanStatus s) => (DocumentAvStatus)(int)s;

    public DocumentService(
        ApplicationDbContext db,
        IFileStorageService storage,
        IAntivirusScanner av,
        IMimeTypeValidator mimeValidator,
        IFilePathBuilder pathBuilder,
        IActivityLogService activityLog,
        INotificationService notifications,
        ILogger<DocumentService> logger)
    {
        _db = db;
        _storage = storage;
        _av = av;
        _mimeValidator = mimeValidator;
        _pathBuilder = pathBuilder;
        _activityLog = activityLog;
        _notifications = notifications;
        _logger = logger;
    }

    // ====================== Upload (FR-001 a FR-012) ======================

    public async Task<UploadResult> UploadAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        string title,
        string? description,
        string category,
        string? tags,
        int? projectId,
        int? taskId,
        int currentUserId,
        string? ipAddress = null,
        CancellationToken ct = default)
    {
        // 1. Validar entrada
        var errors = new List<string>();
        if (fileStream == null) errors.Add("Archivo requerido.");
        if (string.IsNullOrWhiteSpace(fileName)) errors.Add("Nombre de archivo requerido.");
        if (string.IsNullOrWhiteSpace(title)) errors.Add("Título requerido.");
        else if (title.Length > DocumentConstants.MaxTitleLength) errors.Add($"Título máx. {DocumentConstants.MaxTitleLength} chars.");
        if (description?.Length > DocumentConstants.MaxDescriptionLength) errors.Add($"Descripción máx. {DocumentConstants.MaxDescriptionLength} chars.");
        if (string.IsNullOrWhiteSpace(category) || !DocumentConstants.AllowedCategories.Contains(category))
            errors.Add($"Categoría inválida. Permitidas: {string.Join(", ", DocumentConstants.AllowedCategories)}");

        var extension = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();
        if (string.IsNullOrEmpty(extension) || !DocumentConstants.AllowedMimeByExtension.ContainsKey(extension))
            errors.Add($"Extensión no permitida: {extension}");

        if (fileStream != null && fileStream.CanSeek) fileStream.Position = 0;
        if (fileStream != null && fileStream.Length > DocumentConstants.MaxFileSizeBytes)
            errors.Add($"Tamaño máximo permitido: {DocumentConstants.MaxFileSizeBytes / 1024 / 1024} MB.");

        // Validar tags
        if (!string.IsNullOrEmpty(tags))
        {
            var tagList = tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (tagList.Length > DocumentConstants.MaxTags)
                errors.Add($"Máx. {DocumentConstants.MaxTags} tags permitidos (recibidos: {tagList.Length}).");
            foreach (var t in tagList)
                if (t.Length > DocumentConstants.MaxTagLength)
                    errors.Add($"Tag '{t[..Math.Min(t.Length, 20)]}...' excede {DocumentConstants.MaxTagLength} chars.");
        }

        // Validar project access si se especifica
        if (projectId.HasValue)
        {
            var isMember = await IsUserProjectMemberAsync(currentUserId, projectId.Value, ct);
            if (!isMember) errors.Add($"No tienes acceso al proyecto {projectId.Value}.");
        }

        if (errors.Count > 0) throw new DocumentValidationException(errors);

        // 2. Validar MIME magic bytes (anti spoofing — CHK008)
        var detectedMime = await _mimeValidator.ValidateAndDetectAsync(fileStream, extension, ct);
        var declaredMime = DocumentConstants.AllowedMimeByExtension[extension];
        // (La validación lanza InvalidDataException si falla; capturamos abajo)

        // 3. Escanear con antivirus
        ScanResult scanResult;
        if (fileStream.CanSeek) fileStream.Position = 0;
        scanResult = await _av.ScanAsync(fileStream, fileName, ct);

        if (scanResult.Status == ScanStatus.Infected)
        {
            await _activityLog.LogAsync(ActivityLogEvents.DocumentAccessDenied, null, currentUserId, ipAddress,
                new { fileName, threatName = scanResult.ThreatName, reason = "AV infected" }, ct);
            throw new DocumentInfectedException(scanResult.ThreatName);
        }

        // 4. Snapshot del ProjectId si viene de un task
        if (taskId.HasValue && !projectId.HasValue)
        {
            var task = await _db.Tasks.FindAsync(new object?[] { taskId.Value }, ct);
            if (task != null) projectId = task.ProjectId;
        }

        // 5. Generar path seguro
        var relativePath = _pathBuilder.BuildPath(currentUserId.ToString(), projectId, extension);

        // 6. Persistir archivo en disco
        if (fileStream.CanSeek) fileStream.Position = 0;
        try
        {
            await _storage.UploadAsync(fileStream, Path.GetDirectoryName(relativePath)!.Replace('\\', '/'), extension, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo al persistir archivo {FileName}", fileName);
            throw;
        }

        // 7. Insertar en DB con rollback
        var document = new Document
        {
            Title = title.Trim(),
            Description = description?.Trim(),
            Category = category,
            FilePath = relativePath,
            FileSize = fileStream.CanSeek ? fileStream.Length : 0,
            FileType = declaredMime,
            Tags = NormalizeTags(tags),
            UploadedAt = DateTime.UtcNow,
            UploadedByUserId = currentUserId,
            ProjectId = projectId,
            TaskId = taskId,
            AvScanStatus = ToAvStatus(scanResult.Status),
            AvScanAt = DateTime.UtcNow,
            AvThreatName = scanResult.ThreatName,
            OriginalFileName = fileName,
        };

        try
        {
            _db.Documents.Add(document);
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo al insertar Document en DB; haciendo rollback del archivo {Path}", relativePath);
            await SafeDeleteFileAsync(relativePath, ct);
            throw;
        }

        // 8. Log auditoría
        await _activityLog.LogAsync(ActivityLogEvents.DocumentUploaded, document.DocumentId, currentUserId, ipAddress,
            new { fileSize = document.FileSize, mimeType = document.FileType, scanResult = scanResult.Status.ToString() }, ct);

        // 9. Notificar a project members si aplica
        if (projectId.HasValue)
        {
            await NotifyProjectMembersAsync(projectId.Value, currentUserId, document.DocumentId, title, ct);
        }

        return new UploadResult(document.DocumentId, relativePath, scanResult);
    }

    private static string? NormalizeTags(string? tags)
    {
        if (string.IsNullOrWhiteSpace(tags)) return null;
        var normalized = string.Join(",", tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.ToLowerInvariant()));
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }

    private async Task NotifyProjectMembersAsync(int projectId, int senderId, int documentId, string title, CancellationToken ct)
    {
        try
        {
            var members = await _db.ProjectMembers
                .Where(pm => pm.ProjectId == projectId && pm.UserId != senderId)
                .Select(pm => pm.UserId)
                .ToListAsync(ct);
            foreach (var memberId in members)
            {
                var notification = new Notification
                {
                    UserId = memberId,
                    Title = "Nuevo documento en el proyecto",
                    Message = $"Se subió '{title}' al proyecto.",
                    Type = NotificationType.ProjectUpdate,
                    Priority = NotificationPriority.Informational,
                    CreatedDate = DateTime.UtcNow,
                    IsRead = false,
                };
                await _notifications.CreateNotificationAsync(notification);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fallo al notificar project members de proyecto {ProjectId}", projectId);
        }
    }

    private async Task<bool> IsUserProjectMemberAsync(int userId, int projectId, CancellationToken ct)
    {
        var project = await _db.Projects.FindAsync(new object?[] { projectId }, ct);
        if (project == null) return false;
        if (project.ProjectManagerId == userId) return true;
        return await _db.ProjectMembers.AnyAsync(pm => pm.ProjectId == projectId && pm.UserId == userId, ct);
    }

    private async Task SafeDeleteFileAsync(string relativePath, CancellationToken ct)
    {
        try { await _storage.DeleteAsync(relativePath, ct); }
        catch (Exception ex) { _logger.LogError(ex, "Rollback: fallo al eliminar {Path}", relativePath); }
    }

    // ====================== List / Get / Search ======================

    public async Task<PagedResult<DocumentDto>> ListAsync(
        DocumentListFilter filter,
        DocumentSortBy sortBy,
        SortDirection sortDirection,
        int page,
        int pageSize,
        int currentUserId,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 50) pageSize = 25;

        var query = BuildAuthorizedQuery(currentUserId, filter);

        query = sortBy switch
        {
            DocumentSortBy.Title => sortDirection == SortDirection.Asc ? query.OrderBy(d => d.Title) : query.OrderByDescending(d => d.Title),
            DocumentSortBy.Category => sortDirection == SortDirection.Asc ? query.OrderBy(d => d.Category) : query.OrderByDescending(d => d.Category),
            DocumentSortBy.FileSize => sortDirection == SortDirection.Asc ? query.OrderBy(d => d.FileSize) : query.OrderByDescending(d => d.FileSize),
            _ => sortDirection == SortDirection.Asc ? query.OrderBy(d => d.UploadedAt) : query.OrderByDescending(d => d.UploadedAt),
        };

        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        return new PagedResult<DocumentDto>(items.Select(MapToDto).ToList(), total, page, pageSize);
    }

    public async Task<DocumentDto> GetByIdAsync(int documentId, int currentUserId, CancellationToken ct = default)
    {
        var doc = await _db.Documents.FirstOrDefaultAsync(d => d.DocumentId == documentId, ct)
            ?? throw new DocumentNotFoundException(documentId);
        if (!await UserHasAccessAsync(documentId, currentUserId, ct))
            throw new DocumentUnauthorizedAccessException(documentId, currentUserId.ToString());
        return MapToDto(doc);
    }

    public async Task<PagedResult<DocumentDto>> SearchAsync(
        string query, int page, int pageSize, int currentUserId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return new PagedResult<DocumentDto>(Array.Empty<DocumentDto>(), 0, page, pageSize);

        var filter = new DocumentListFilter(SearchTerm: query);
        var baseQuery = BuildAuthorizedQuery(currentUserId, filter);
        var total = await baseQuery.CountAsync(ct);
        var items = await baseQuery.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<DocumentDto>(items.Select(MapToDto).ToList(), total, page, pageSize);
    }

    private IQueryable<Document> BuildAuthorizedQuery(int currentUserId, DocumentListFilter filter)
    {
        var q = _db.Documents.AsNoTracking().AsQueryable();

        // Authorization: owner OR member of project OR shared
        var myProjectIds = _db.ProjectMembers.Where(pm => pm.UserId == currentUserId).Select(pm => pm.ProjectId);
        var mySharedDocIds = _db.DocumentShares
            .Where(s => s.SharedWithUserId == currentUserId && s.RevokedAt == null
                && (s.ExpiresAt == null || s.ExpiresAt > DateTime.UtcNow))
            .Select(s => s.DocumentId);

        q = q.Where(d => d.UploadedByUserId == currentUserId
            || (d.ProjectId != null && myProjectIds.Contains(d.ProjectId.Value))
            || mySharedDocIds.Contains(d.DocumentId));

        if (filter.ProjectId.HasValue) q = q.Where(d => d.ProjectId == filter.ProjectId);
        if (!string.IsNullOrEmpty(filter.Category)) q = q.Where(d => d.Category == filter.Category);
        if (filter.UploadedAfter.HasValue) q = q.Where(d => d.UploadedAt >= filter.UploadedAfter);
        if (filter.UploadedBefore.HasValue) q = q.Where(d => d.UploadedAt <= filter.UploadedBefore);
        if (filter.UploadedByUserId.HasValue) q = q.Where(d => d.UploadedByUserId == filter.UploadedByUserId);
        if (!string.IsNullOrEmpty(filter.SearchTerm))
        {
            var term = $"%{filter.SearchTerm}%";
            q = q.Where(d => EF.Functions.Like(d.Title, term)
                          || (d.Description != null && EF.Functions.Like(d.Description, term))
                          || (d.Tags != null && EF.Functions.Like(d.Tags, term)));
        }

        return q;
    }

    public async Task<bool> UserHasAccessAsync(int documentId, int userId, CancellationToken ct = default)
    {
        var doc = await _db.Documents.AsNoTracking()
            .FirstOrDefaultAsync(d => d.DocumentId == documentId, ct);
        if (doc == null) return false;
        if (doc.UploadedByUserId == userId) return true;
        if (doc.ProjectId.HasValue)
        {
            var project = await _db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.ProjectId == doc.ProjectId, ct);
            if (project?.ProjectManagerId == userId) return true;
            if (await _db.ProjectMembers.AsNoTracking().AnyAsync(pm => pm.ProjectId == doc.ProjectId && pm.UserId == userId, ct))
                return true;
        }
        return await _db.DocumentShares.AsNoTracking().AnyAsync(s =>
            s.DocumentId == documentId
            && s.SharedWithUserId == userId
            && s.RevokedAt == null
            && (s.ExpiresAt == null || s.ExpiresAt > DateTime.UtcNow), ct);
    }

    // ====================== Download / Replace / Delete / Update ======================

    public async Task<(Stream Stream, string FileName, string ContentType)> OpenForDownloadAsync(
        int documentId, int currentUserId, CancellationToken ct = default)
    {
        var doc = await GetByIdAsync(documentId, currentUserId, ct);
        var stream = await _storage.DownloadAsync(doc.FilePath, ct);

        // Log asíncrono (fire-and-forget) — no bloqueamos la descarga
        _ = _activityLog.LogAsync(ActivityLogEvents.DocumentDownloaded, documentId, currentUserId, null,
            new { fileName = doc.OriginalFileName, fileSize = doc.FileSize }, CancellationToken.None);

        return (stream, doc.OriginalFileName, doc.FileType);
    }

    public async Task<DocumentDto> UpdateMetadataAsync(
        int documentId, string? newTitle, string? newDescription, string? newCategory, string? newTags,
        int currentUserId, CancellationToken ct = default)
    {
        var doc = await _db.Documents.FirstOrDefaultAsync(d => d.DocumentId == documentId, ct)
            ?? throw new DocumentNotFoundException(documentId);
        if (doc.UploadedByUserId != currentUserId)
            throw new DocumentUnauthorizedAccessException(documentId, currentUserId.ToString());

        if (!string.IsNullOrWhiteSpace(newTitle)) doc.Title = newTitle.Trim();
        if (newDescription != null) doc.Description = newDescription.Trim();
        if (!string.IsNullOrWhiteSpace(newCategory))
        {
            if (!DocumentConstants.AllowedCategories.Contains(newCategory))
                throw new DocumentValidationException(new[] { $"Categoría inválida: {newCategory}" });
            doc.Category = newCategory;
        }
        if (newTags != null) doc.Tags = NormalizeTags(newTags);

        await _db.SaveChangesAsync(ct);
        return MapToDto(doc);
    }

    public async Task<DocumentDto> ReplaceFileAsync(
        int documentId, Stream newFileStream, string newFileName, string newContentType,
        int currentUserId, string? ipAddress = null, CancellationToken ct = default)
    {
        var doc = await _db.Documents.FirstOrDefaultAsync(d => d.DocumentId == documentId, ct)
            ?? throw new DocumentNotFoundException(documentId);
        if (doc.UploadedByUserId != currentUserId)
            throw new DocumentUnauthorizedAccessException(documentId, currentUserId.ToString());

        var oldPath = doc.FilePath;
        var extension = Path.GetExtension(newFileName).TrimStart('.').ToLowerInvariant();
        if (!DocumentConstants.AllowedMimeByExtension.ContainsKey(extension))
            throw new DocumentValidationException(new[] { $"Extensión no permitida: {extension}" });

        // Scan
        if (newFileStream.CanSeek) newFileStream.Position = 0;
        var scan = await _av.ScanAsync(newFileStream, newFileName, ct);
        if (scan.Status == ScanStatus.Infected)
            throw new DocumentInfectedException(scan.ThreatName);

        // New GUID (last-writer-wins: per Clarifications Q3)
        var newPath = _pathBuilder.BuildPath(currentUserId.ToString(), doc.ProjectId, extension);

        if (newFileStream.CanSeek) newFileStream.Position = 0;
        var newDir = Path.GetDirectoryName(newPath)!.Replace('\\', '/');
        await _storage.UploadAsync(newFileStream, newDir, extension, ct);

        var oldSize = doc.FileSize;
        doc.FilePath = newPath;
        doc.FileSize = newFileStream.CanSeek ? newFileStream.Length : 0;
        doc.FileType = DocumentConstants.AllowedMimeByExtension[extension];
        doc.OriginalFileName = newFileName;
        doc.ReplacedAt = DateTime.UtcNow;
        doc.AvScanStatus = ToAvStatus(scan.Status);
        doc.AvScanAt = DateTime.UtcNow;
        doc.AvThreatName = scan.ThreatName;
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch
        {
            await SafeDeleteFileAsync(newPath, ct);
            throw;
        }

        // Eliminar archivo antiguo (best-effort)
        await SafeDeleteFileAsync(oldPath, ct);

        await _activityLog.LogAsync(ActivityLogEvents.DocumentReplaced, documentId, currentUserId, ipAddress,
            new { oldPath, newPath, oldSize, newSize = doc.FileSize }, ct);

        return MapToDto(doc);
    }

    public async Task DeleteAsync(int documentId, int currentUserId, string? ipAddress = null, CancellationToken ct = default)
    {
        var doc = await _db.Documents.FirstOrDefaultAsync(d => d.DocumentId == documentId, ct)
            ?? throw new DocumentNotFoundException(documentId);

        // Authorization: owner OR PM of project
        var isOwner = doc.UploadedByUserId == currentUserId;
        var isPm = doc.ProjectId.HasValue && await _db.Projects.AnyAsync(p => p.ProjectId == doc.ProjectId && p.ProjectManagerId == currentUserId, ct);
        if (!isOwner && !isPm)
            throw new DocumentUnauthorizedAccessException(documentId, currentUserId.ToString());

        var path = doc.FilePath;
        _db.Documents.Remove(doc);
        await _db.SaveChangesAsync(ct);

        await SafeDeleteFileAsync(path, ct);
        await _activityLog.LogAsync(ActivityLogEvents.DocumentDeleted, documentId, currentUserId, ipAddress,
            new { filePath = path, deletedBy = isOwner ? "owner" : "pm" }, ct);
    }

    // ====================== Mapping ======================

    private static DocumentDto MapToDto(Document d) => new(
        d.DocumentId, d.Title, d.Description, d.Category, d.FilePath, d.FileSize, d.FileType,
        d.Tags, d.UploadedAt, d.UploadedByUserId, d.ProjectId, d.TaskId, d.ReplacedAt,
        d.AvScanStatus, d.AvThreatName, d.OriginalFileName);
}
