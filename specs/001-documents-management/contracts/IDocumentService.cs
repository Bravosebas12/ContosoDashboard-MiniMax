using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ContosoDashboard.Models;

namespace ContosoDashboard.Services.Documents;

/// <summary>
/// DTOs públicos de <see cref="IDocumentService"/>. Se usan records inmutables
/// para evitar mutaciones accidentales entre capas (cumple Constitución IV).
/// </summary>
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
    string UploadedByUserId,
    int? ProjectId,
    int? TaskId,
    DateTime? ReplacedAt,
    ScanStatus AvScanStatus,
    string OriginalFileName);

public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasNext => Page < TotalPages;
    public bool HasPrevious => Page > 1;
}

public record UploadResult(
    int DocumentId,
    string FilePath,
    ScanResult ScanResult);

public enum DocumentSortBy
{
    Title,
    UploadedAt,
    Category,
    FileSize
}

public enum SortDirection
{
    Asc,
    Desc
}

/// <summary>
/// Filtros para listar documentos. Todos los campos son opcionales; null = sin filtro.
/// </summary>
public record DocumentListFilter(
    int? ProjectId = null,
    string? Category = null,
    DateTime? UploadedAfter = null,
    DateTime? UploadedBefore = null,
    string? UploadedByUserId = null);

/// <summary>
/// Servicio de dominio para la gestión de documentos. Centraliza:
/// - Validación de entrada (extensión, tamaño, MIME, owner)
/// - Orquestación del upload (AV → disco → DB → rollback)
/// - Aplicación de permisos (defense in depth — ver FR-033, FR-035)
/// - Auditoría estructurada (ver FR-029)
/// </summary>
public interface IDocumentService
{
    /// <summary>
    /// Sube un nuevo documento. Realiza validación, escaneo AV, persistencia en
    /// disco, inserción en DB con rollback automático, y notificación a project
    /// members si aplica. Ver FR-001 a FR-012.
    /// </summary>
    /// <param name="fileStream">Contenido del archivo (posicionado en 0).</param>
    /// <param name="fileName">Nombre ORIGINAL del archivo (para `OriginalFileName`).</param>
    /// <param name="contentType">MIME type reportado por el cliente (se valida contra whitelist).</param>
    /// <param name="title">Título (1-200 chars, validado).</param>
    /// <param name="description">Descripción opcional (0-2000 chars).</param>
    /// <param name="category">Categoría (debe estar en la whitelist de 6).</param>
    /// <param name="tags">Tags opcionales (se normalizan a lowercase; max 5 × 50 chars).</param>
    /// <param name="projectId">Proyecto opcional. Si se especifica, se valida que el usuario
    /// tiene acceso al proyecto (es miembro o PM).</param>
    /// <param name="taskId">Tarea opcional. Si se especifica, se snapshot del ProjectId.</param>
    /// <param name="currentUserId">ID del usuario actual (de la claim `NameIdentifier`).</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Resultado con el `DocumentId` creado, el `FilePath` y el resultado del AV.</returns>
    /// <exception cref="ValidationException">Si alguna validación falla (extensión, tamaño, MIME, etc.).</exception>
    /// <exception cref="UnauthorizedAccessException">Si el usuario no tiene acceso al proyecto.</exception>
    /// <exception cref="InvalidScanResultException">Si el AV detecta infección.</exception>
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
        string currentUserId,
        CancellationToken ct = default);

    /// <summary>
    /// Lista documentos visibles para el usuario, con paginación y ordenamiento.
    /// Aplica filtro de autorización (solo docs propios, de sus proyectos, o compartidos con él).
    /// </summary>
    Task<PagedResult<DocumentDto>> ListAsync(
        DocumentListFilter filter,
        DocumentSortBy sortBy,
        SortDirection sortDirection,
        int page,
        int pageSize,
        string currentUserId,
        CancellationToken ct = default);

    /// <summary>
    /// Devuelve un documento por ID, validando que el usuario tiene acceso.
    /// </summary>
    /// <exception cref="NotFoundException">Si el documento no existe.</exception>
    /// <exception cref="UnauthorizedAccessException">Si el usuario no tiene acceso.</exception>
    Task<DocumentDto> GetByIdAsync(int documentId, string currentUserId, CancellationToken ct = default);

    /// <summary>
    /// Devuelve el stream para descargar el archivo, validando acceso.
    /// </summary>
    /// <exception cref="NotFoundException">Si el documento no existe.</exception>
    /// <exception cref="UnauthorizedAccessException">Si el usuario no tiene acceso.</exception>
    /// <exception cref="InvalidOperationException">Si el archivo no está físicamente en disco.</exception>
    Task<(Stream Stream, string FileName, string ContentType)> OpenForDownloadAsync(
        int documentId, string currentUserId, CancellationToken ct = default);

    /// <summary>
    /// Actualiza la metadata de un documento. Solo el dueño (uploader) puede hacerlo.
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">Si el usuario no es el dueño.</exception>
    Task<DocumentDto> UpdateMetadataAsync(
        int documentId,
        string? newTitle,
        string? newDescription,
        string? newCategory,
        string? newTags,
        string currentUserId,
        CancellationToken ct = default);

    /// <summary>
    /// Reemplaza el archivo físico manteniendo el `DocumentId` y el path lógico.
    /// Genera un nuevo GUID, elimina el archivo antiguo, actualiza `ReplacedAt` y metadata.
    /// Concurrencia: last-writer-wins (ver Clarifications Q3) — la descarga en curso
    /// completa con el archivo antiguo; la próxima GET ve el archivo nuevo.
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">Si el usuario no es el dueño.</exception>
    Task<DocumentDto> ReplaceFileAsync(
        int documentId,
        Stream newFileStream,
        string newFileName,
        string newContentType,
        string currentUserId,
        CancellationToken ct = default);

    /// <summary>
    /// Elimina un documento. Dueño siempre puede; Project Manager puede si el documento
    /// es de su proyecto. Cascade a `DocumentShare`. Hard delete (no soft delete).
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">Si el usuario no es dueño ni PM del proyecto.</exception>
    Task DeleteAsync(int documentId, string currentUserId, CancellationToken ct = default);

    /// <summary>
    /// Búsqueda full-text sobre Title + Description + Tags. Solo devuelve documentos
    /// visibles para el usuario.
    /// </summary>
    Task<PagedResult<DocumentDto>> SearchAsync(
        string query,
        int page,
        int pageSize,
        string currentUserId,
        CancellationToken ct = default);
}

/// <summary>
/// Excepciones específicas del dominio de documentos.
/// </summary>
public class DocumentNotFoundException : Exception
{
    public DocumentNotFoundException(int documentId)
        : base($"Documento {documentId} no encontrado.") { }
}

public class DocumentUnauthorizedAccessException : Exception
{
    public DocumentUnauthorizedAccessException(int documentId, string userId)
        : base($"Usuario '{userId}' no tiene acceso al documento {documentId}.") { }
}

public class DocumentValidationException : Exception
{
    public IReadOnlyList<string> Errors { get; }
    public DocumentValidationException(IReadOnlyList<string> errors)
        : base("Validación fallida: " + string.Join("; ", errors))
    {
        Errors = errors;
    }
}

public class DocumentInfectedException : Exception
{
    public string ThreatName { get; }
    public DocumentInfectedException(string threatName)
        : base($"Archivo rechazado: amenaza detectada ({threatName}).")
    {
        ThreatName = threatName;
    }
}
