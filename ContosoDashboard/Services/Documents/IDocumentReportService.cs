using System.Collections.Generic;

namespace ContosoDashboard.Services.Documents;

/// <summary>
/// Tipos de reporte administrativo sobre documentos (per AC-6.2.1, FR-030).
/// </summary>
public enum DocumentReportType
{
    /// <summary>Top 10 tipos MIME por cantidad de uploads (CSV).</summary>
    MimeTypes = 0,
    /// <summary>Top 10 usuarios por cantidad de uploads (CSV).</summary>
    Uploaders = 1,
}

/// <summary>
/// Fila de reporte administrativo (un renglon en el CSV).
/// </summary>
/// <param name="Rank">Posicion 1..10 (per FR-030).</param>
/// <param name="Key">Identificador del grupo (MIME type, user id, etc.).</param>
/// <param name="Label">Etiqueta legible (ej. "application/pdf" o "Camille Nicole").</param>
/// <param name="Count">Cantidad de eventos contados.</param>
public record DocumentReportRow(int Rank, string Key, string Label, long Count);

public interface IDocumentReportService
{
    /// <summary>
    /// Genera un CSV con el top 10 del tipo de reporte solicitado.
    /// Solo accesible para usuarios con rol Administrator.
    /// </summary>
    /// <param name="type">Tipo de reporte (MIME types o uploaders).</param>
    /// <returns>String con el CSV formateado (encabezados + 10 filas + BOM UTF-8 opcional).</returns>
    Task<string> GenerateCsvAsync(DocumentReportType type, CancellationToken ct = default);

    /// <summary>
    /// Retorna el top 10 de tipos MIME por uploads. Util para renderizar UI ademas del CSV.
    /// </summary>
    Task<IReadOnlyList<DocumentReportRow>> GetTopMimeTypesAsync(int top = 10, CancellationToken ct = default);

    /// <summary>Retorna el top 10 de usuarios por cantidad de uploads.</summary>
    Task<IReadOnlyList<DocumentReportRow>> GetTopUploadersAsync(int top = 10, CancellationToken ct = default);
}
