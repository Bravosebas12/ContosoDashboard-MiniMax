using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ContosoDashboard.Models;

namespace ContosoDashboard.Services.Documents;

// ====================== DTOs ======================

public record DocumentShareDto(
    int DocumentShareId,
    int DocumentId,
    int? SharedWithUserId,
    string? SharedWithRole,
    DocumentSharePermission Permission,
    DateTime SharedAt,
    int SharedByUserId,
    DateTime? ExpiresAt,
    DateTime? RevokedAt);

public record ShareRequest(
    int DocumentId,
    int TargetUserId,
    DocumentSharePermission Permission,
    DateTime? ExpiresAt);

public record ShareGrantResult(
    int DocumentShareId,
    bool NotificationDelivered);

public record ActiveShareInfo(
    int DocumentShareId,
    int SharedWithUserId,
    string SharedWithUserDisplayName,
    DocumentSharePermission Permission,
    DateTime SharedAt,
    DateTime? ExpiresAt);

// ====================== Interface ======================

/// <summary>
/// Servicio de dominio para compartir y revocar acceso a documentos.
/// Reglas de negocio (per Clarifications Q1):
/// - El dueño (uploader) puede compartir con cualquier usuario de la organización.
/// - Un Project Manager solo puede compartir dentro de su proyecto.
/// - Otros roles NO pueden compartir.
/// Ver <c>specs/001-documents-management/contracts/IDocumentShareService.cs</c>.
/// </summary>
public interface IDocumentShareService
{
    /// <summary>Comparte un documento con un usuario. Aplica las 3 reglas de autorización.</summary>
    Task<ShareGrantResult> ShareAsync(ShareRequest request, int currentUserId, CancellationToken ct = default);

    /// <summary>Revoca un share (soft delete via RevokedAt). Solo el dueño puede revocar.</summary>
    Task RevokeAsync(int documentShareId, int currentUserId, CancellationToken ct = default);

    /// <summary>Lista los shares activos de un documento. Solo el dueño puede ver la lista completa.</summary>
    Task<IReadOnlyList<ActiveShareInfo>> ListActiveSharesAsync(int documentId, int currentUserId, CancellationToken ct = default);

    /// <summary>Lista los documentos compartidos con el usuario (no expirados, no revocados).</summary>
    Task<PagedResult<DocumentDto>> ListSharedWithMeAsync(int page, int pageSize, int currentUserId, CancellationToken ct = default);

    /// <summary>Verifica si el usuario tiene acceso (owner, project member, o active share).</summary>
    Task<bool> UserHasAccessAsync(int documentId, int userId, CancellationToken ct = default);
}
