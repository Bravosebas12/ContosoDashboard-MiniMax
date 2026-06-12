using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ContosoDashboard.Data;
using ContosoDashboard.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ContosoDashboard.Services.Documents;

/// <summary>
/// Implementación de <see cref="IDocumentShareService"/>.
/// Reglas de autorización (per Clarifications Q1):
/// 1. Owner (uploader) puede compartir con cualquier usuario activo de la organización.
/// 2. Project Manager solo puede compartir dentro de su proyecto (target debe ser miembro).
/// 3. Otros roles NO pueden compartir.
/// </summary>
public class DocumentShareService : IDocumentShareService
{
    private readonly ApplicationDbContext _db;
    private readonly INotificationService _notifications;
    private readonly IActivityLogService _activityLog;
    private readonly ILogger<DocumentShareService> _logger;

    public DocumentShareService(
        ApplicationDbContext db,
        INotificationService notifications,
        IActivityLogService activityLog,
        ILogger<DocumentShareService> logger)
    {
        _db = db;
        _notifications = notifications;
        _activityLog = activityLog;
        _logger = logger;
    }

    public async Task<ShareGrantResult> ShareAsync(ShareRequest request, int currentUserId, CancellationToken ct = default)
    {
        // 1. Validar inputs
        if (request.TargetUserId == currentUserId)
            throw new InvalidOperationException("No puedes compartir un documento contigo mismo.");

        // Verificar target existe y está activo
        var targetUser = await _db.Users.FindAsync(new object?[] { request.TargetUserId }, ct);
        if (targetUser == null)
            throw new InvalidOperationException($"Usuario destino {request.TargetUserId} no existe.");

        // 2. Verificar el documento
        var doc = await _db.Documents.FindAsync(new object?[] { request.DocumentId }, ct)
            ?? throw new DocumentNotFoundException(request.DocumentId);

        // 3. Verificar autorización (las 3 reglas)
        var isOwner = doc.UploadedByUserId == currentUserId;
        var isProjectManager = false;
        if (doc.ProjectId.HasValue)
        {
            var project = await _db.Projects.AsNoTracking()
                .FirstOrDefaultAsync(p => p.ProjectId == doc.ProjectId, ct);
            isProjectManager = project?.ProjectManagerId == currentUserId;
        }

        if (!isOwner && !isProjectManager)
            throw new DocumentUnauthorizedAccessException(request.DocumentId, currentUserId.ToString());

        // 4. Si es PM (no owner), verificar que target está en el mismo proyecto
        if (!isOwner && isProjectManager && doc.ProjectId.HasValue)
        {
            var isTargetInProject = await _db.ProjectMembers.AsNoTracking().AnyAsync(
                pm => pm.ProjectId == doc.ProjectId && pm.UserId == request.TargetUserId, ct);
            if (!isTargetInProject)
                throw new DocumentValidationException(new[]
                {
                    $"El PM solo puede compartir dentro del proyecto. El usuario {request.TargetUserId} no es miembro del proyecto {doc.ProjectId}."
                });
        }

        // 5. Verificar que no existe ya un share activo
        var existing = await _db.DocumentShares.FirstOrDefaultAsync(s =>
            s.DocumentId == request.DocumentId
            && s.SharedWithUserId == request.TargetUserId
            && s.RevokedAt == null
            && (s.ExpiresAt == null || s.ExpiresAt > DateTime.UtcNow), ct);
        if (existing != null)
            throw new InvalidOperationException($"Ya existe un share activo para este usuario.");

        // 6. Crear DocumentShare
        var share = new DocumentShare
        {
            DocumentId = request.DocumentId,
            SharedWithUserId = request.TargetUserId,
            Permission = request.Permission,
            SharedAt = DateTime.UtcNow,
            SharedByUserId = currentUserId,
            ExpiresAt = request.ExpiresAt,
        };
        _db.DocumentShares.Add(share);
        await _db.SaveChangesAsync(ct);

        // 7. Enviar notificación al receptor (via queue, resuelve A2/A3).
        bool notificationDelivered = false;
        try
        {
            var notification = new Notification
            {
                UserId = request.TargetUserId,
                Title = "Documento compartido contigo",
                Message = $"'{doc.Title}' ha sido compartido contigo.",
                Type = NotificationType.ProjectUpdate,
                Priority = NotificationPriority.Informational,
                CreatedDate = DateTime.UtcNow,
                IsRead = false,
            };
            await _notifications.EnqueueAsync(notification, ct);
            notificationDelivered = true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fallo al enqueuear notificación al usuario {UserId} del share del documento {DocumentId}", request.TargetUserId, request.DocumentId);
        }

        // 8. Log auditoría
        await _activityLog.LogAsync(ActivityLogEvents.DocumentShared, request.DocumentId, currentUserId, null,
            new { sharedWith = request.TargetUserId, permission = request.Permission.ToString(), expiresAt = request.ExpiresAt }, ct);

        return new ShareGrantResult(share.DocumentShareId, notificationDelivered);
    }

    public async Task RevokeAsync(int documentShareId, int currentUserId, CancellationToken ct = default)
    {
        var share = await _db.DocumentShares.FirstOrDefaultAsync(s => s.DocumentShareId == documentShareId, ct)
            ?? throw new DocumentNotFoundException(documentShareId);

        if (share.RevokedAt != null)
            throw new InvalidOperationException("Este share ya fue revocado.");

        if (share.SharedByUserId != currentUserId)
        {
            // Verificar si el currentUser es el dueño del documento (no del share)
            var doc = await _db.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.DocumentId == share.DocumentId, ct);
            if (doc == null || doc.UploadedByUserId != currentUserId)
                throw new DocumentUnauthorizedAccessException(share.DocumentId, currentUserId.ToString());
        }

        share.RevokedAt = DateTime.UtcNow;
        share.RevokedByUserId = currentUserId;
        await _db.SaveChangesAsync(ct);

        // Notificar al receptor (via queue, resuelve A2/A3)
        try
        {
            if (share.SharedWithUserId.HasValue)
            {
                var doc = await _db.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.DocumentId == share.DocumentId, ct);
                var notification = new Notification
                {
                    UserId = share.SharedWithUserId.Value,
                    Title = "Acceso a documento revocado",
                    Message = $"Tu acceso a '{doc?.Title ?? "el documento"}' ha sido revocado.",
                    Type = NotificationType.ProjectUpdate,
                    Priority = NotificationPriority.Informational,
                    CreatedDate = DateTime.UtcNow,
                    IsRead = false,
                };
                await _notifications.EnqueueAsync(notification, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fallo al enqueuear notificación de revocación de share {ShareId}", documentShareId);
        }

        await _activityLog.LogAsync(ActivityLogEvents.DocumentRevoked, share.DocumentId, currentUserId, null,
            new { shareId = documentShareId, revokedFrom = share.SharedWithUserId }, ct);
    }

    public async Task<IReadOnlyList<ActiveShareInfo>> ListActiveSharesAsync(int documentId, int currentUserId, CancellationToken ct = default)
    {
        var doc = await _db.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.DocumentId == documentId, ct);
        if (doc == null) return Array.Empty<ActiveShareInfo>();
        if (doc.UploadedByUserId != currentUserId)
            throw new DocumentUnauthorizedAccessException(documentId, currentUserId.ToString());

        return await _db.DocumentShares.AsNoTracking()
            .Where(s => s.DocumentId == documentId && s.RevokedAt == null
                && (s.ExpiresAt == null || s.ExpiresAt > DateTime.UtcNow))
            .Join(_db.Users, s => s.SharedWithUserId, u => u.UserId, (s, u) => new ActiveShareInfo(
                s.DocumentShareId, s.SharedWithUserId!.Value, u.DisplayName,
                s.Permission, s.SharedAt, s.ExpiresAt))
            .ToListAsync(ct);
    }

    public async Task<PagedResult<DocumentDto>> ListSharedWithMeAsync(int page, int pageSize, int currentUserId, CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 50) pageSize = 25;

        var q = _db.DocumentShares.AsNoTracking()
            .Where(s => s.SharedWithUserId == currentUserId && s.RevokedAt == null
                && (s.ExpiresAt == null || s.ExpiresAt > DateTime.UtcNow))
            .Join(_db.Documents.AsNoTracking(), s => s.DocumentId, d => d.DocumentId, (s, d) => d);

        var total = await q.CountAsync(ct);
        var docs = await q.OrderByDescending(d => d.UploadedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<DocumentDto>(docs.Select(d => new DocumentDto(
            d.DocumentId, d.Title, d.Description, d.Category, d.FilePath, d.FileSize, d.FileType,
            d.Tags, d.UploadedAt, d.UploadedByUserId, d.ProjectId, d.TaskId, d.ReplacedAt,
            d.AvScanStatus, d.AvThreatName, d.OriginalFileName)).ToList(), total, page, pageSize);
    }

    public async Task<bool> UserHasAccessAsync(int documentId, int userId, CancellationToken ct = default)
    {
        var doc = await _db.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.DocumentId == documentId, ct);
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
}
