using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ContosoDashboard.Models;

namespace ContosoDashboard.Services.Documents;

public enum SharePermission
{
    Read = 0,
    Write = 1
}

public record DocumentShareDto(
    int DocumentShareId,
    int DocumentId,
    string? SharedWithUserId,
    string? SharedWithRole,
    SharePermission Permission,
    DateTime SharedAt,
    string SharedByUserId,
    DateTime? ExpiresAt,
    DateTime? RevokedAt);

public record ShareRequest(
    int DocumentId,
    string TargetUserId,       // Para futuras: string? TargetRole
    SharePermission Permission,
    DateTime? ExpiresAt);

public record ShareGrantResult(
    int DocumentShareId,
    bool NotificationDelivered);

/// <summary>
/// Servicio de dominio para compartir y revocar acceso a documentos.
/// Encapsula las reglas de negocio de FR-023 a FR-025, FR-035.
/// </summary>
public interface IDocumentShareService
{
    /// <summary>
    /// Comparte un documento con un usuario. Aplica las reglas de autorización:
    /// - El dueño (uploader) puede compartir con cualquier usuario de la organización.
    /// - Un Project Manager solo puede compartir dentro de su proyecto.
    /// - Otros roles NO pueden compartir.
    /// Envía notificación in-app al receptor en ≤ 5s (via INotificationService).
    /// </summary>
    /// <exception cref="DocumentNotFoundException">Si el documento no existe.</exception>
    /// <exception cref="DocumentUnauthorizedAccessException">Si el caller no es el dueño
    /// ni PM del proyecto, o si la compartición viola las reglas de FR-035.</exception>
    /// <exception cref="InvalidOperationException">Si el targetUserId es el dueño mismo
    /// o si ya existe un share activo (duplicado).</exception>
    Task<ShareGrantResult> ShareAsync(ShareRequest request, string currentUserId, CancellationToken ct = default);

    /// <summary>
    /// Revoca un share existente (soft delete — set RevokedAt). Solo el dueño
    /// del documento (uploader) puede revocar.
    /// </summary>
    /// <exception cref="DocumentUnauthorizedAccessException">Si el caller no es el dueño.</exception>
    Task RevokeAsync(int documentShareId, string currentUserId, CancellationToken ct = default);

    /// <summary>
    /// Lista los shares activos (no revocados, no expirados) de un documento.
    /// Solo el dueño del documento puede ver la lista completa.
    /// </summary>
    Task<IReadOnlyList<DocumentShareDto>> ListActiveSharesAsync(int documentId, string currentUserId, CancellationToken ct = default);

    /// <summary>
    /// Lista los documentos compartidos con el usuario actual (no expirados, no revocados).
    /// Se usa en la vista "Compartido conmigo".
    /// </summary>
    Task<PagedResult<DocumentDto>> ListSharedWithMeAsync(int page, int pageSize, string currentUserId, CancellationToken ct = default);

    /// <summary>
    /// Verifica si el usuario tiene acceso a un documento (es dueño, miembro del proyecto,
    /// o tiene un share activo). Usado por el `DocumentService` antes de cada operación
    /// (defense in depth — FR-033).
    /// </summary>
    Task<bool> UserHasAccessAsync(int documentId, string userId, CancellationToken ct = default);
}
