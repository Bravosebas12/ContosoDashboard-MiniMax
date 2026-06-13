using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ContosoDashboard.Models;

namespace ContosoDashboard.Services.Documents;

// ====================== DTOs públicos ======================

/// <summary>DTO público de un documento. Se devuelve a Blazor; nunca exponer la entity EF directamente.</summary>
public record DocumentDto(
    int DocumentId,
    string Title,
    string? Description,
    string Category,
    string FilePath,
    long FileSize,
    string FileType,
    string? Tags,
    DateTime UploadedAt,
    int UploadedByUserId,
    int? ProjectId,
    int? TaskId,
    DateTime? ReplacedAt,
    DocumentAvStatus AvScanStatus,
    string? AvThreatName,
    string OriginalFileName);

public record UploadResult(
    int DocumentId,
    string FilePath,
    ScanResult ScanResult);

public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasNext => Page < TotalPages;
    public bool HasPrevious => Page > 1;
}

public record DocumentListFilter(
    int? ProjectId = null,
    string? Category = null,
    DateTime? UploadedAfter = null,
    DateTime? UploadedBefore = null,
    int? UploadedByUserId = null,
    string? SearchTerm = null);

public enum DocumentSortBy
{
    Title = 0,
    UploadedAt = 1,
    Category = 2,
    FileSize = 3
}

public enum SortDirection
{
    Asc = 0,
    Desc = 1
}

// ====================== Excepciones ======================

public class DocumentNotFoundException : Exception
{
    public int DocumentId { get; }
    public DocumentNotFoundException(int documentId)
        : base($"Document {documentId} not found.") => DocumentId = documentId;
}

public class DocumentUnauthorizedAccessException : Exception
{
    public int DocumentId { get; }
    public string UserId { get; }
    public DocumentUnauthorizedAccessException(int documentId, string userId)
        : base($"User '{userId}' does not have access to document {documentId}.")
    {
        DocumentId = documentId;
        UserId = userId;
    }
}

public class DocumentValidationException : Exception
{
    public IReadOnlyList<string> Errors { get; }
    public DocumentValidationException(IReadOnlyList<string> errors)
        : base("Validation failed: " + string.Join("; ", errors)) => Errors = errors;
}

public class DocumentInfectedException : Exception
{
    public string? ThreatName { get; }
    public DocumentInfectedException(string? threatName)
        : base($"File rejected: threat detected ({threatName ?? "unknown"}).")
        => ThreatName = threatName;
}

// ====================== Interface ======================

/// <summary>
/// Servicio de dominio para gestión de documentos. Centraliza:
/// - Validación de entrada (extensión, tamaño, MIME, owner)
/// - Orquestación del upload (AV → disco → DB → rollback)
/// - Aplicación de permisos (defense in depth — ver FR-033, FR-035)
/// - Auditoría estructurada (ver FR-029)
/// Ver `specs/001-documents-management/spec.md` y `data-model.md` para documentación completa.
/// </summary>
public interface IDocumentService
{
    /// <summary>Sube un nuevo documento. Realiza validación, AV scan, persistencia con rollback.</summary>
    Task<UploadResult> UploadAsync(
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
        CancellationToken ct = default);

    /// <summary>Lista documentos visibles para el usuario, paginados.</summary>
    Task<PagedResult<DocumentDto>> ListAsync(
        DocumentListFilter filter,
        DocumentSortBy sortBy,
        SortDirection sortDirection,
        int page,
        int pageSize,
        int currentUserId,
        CancellationToken ct = default);

    /// <summary>Obtiene un documento por ID, validando acceso.</summary>
    Task<DocumentDto> GetByIdAsync(int documentId, int currentUserId, CancellationToken ct = default);

    /// <summary>Abre un documento para descarga, retornando stream + metadata.</summary>
    Task<(Stream Stream, string FileName, string ContentType)> OpenForDownloadAsync(
        int documentId, int currentUserId, CancellationToken ct = default);

    /// <summary>Actualiza metadata (solo el dueño).</summary>
    Task<DocumentDto> UpdateMetadataAsync(
        int documentId,
        string? newTitle,
        string? newDescription,
        string? newCategory,
        string? newTags,
        int currentUserId,
        CancellationToken ct = default);

    /// <summary>Reemplaza el archivo (solo el dueño). Genera nuevo GUID, elimina antiguo.</summary>
    Task<DocumentDto> ReplaceFileAsync(
        int documentId,
        Stream newFileStream,
        string newFileName,
        string newContentType,
        int currentUserId,
        string? ipAddress = null,
        CancellationToken ct = default);

    /// <summary>Elimina un documento (dueño o PM del proyecto).</summary>
    Task DeleteAsync(int documentId, int currentUserId, string? ipAddress = null, CancellationToken ct = default);

    /// <summary>Búsqueda full-text sobre Title + Description + Tags. Solo documentos visibles.</summary>
    Task<PagedResult<DocumentDto>> SearchAsync(
        string query,
        int page,
        int pageSize,
        int currentUserId,
        CancellationToken ct = default);

    /// <summary>Indica si el usuario tiene acceso a un documento (defense in depth layer 3).</summary>
    Task<bool> UserHasAccessAsync(int documentId, int userId, CancellationToken ct = default);
}
