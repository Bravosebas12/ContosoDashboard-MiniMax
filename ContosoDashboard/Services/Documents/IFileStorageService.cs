using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ContosoDashboard.Services.Documents;

/// <summary>
/// Abstracción del backend de almacenamiento de archivos. Esta release implementa
/// únicamente <see cref="LocalFileStorageService"/> (filesystem local). Otras
/// implementaciones NO forman parte de esta release (ver FR-038 y Clarifications Q2).
/// </summary>
/// <remarks>
/// Responsabilidad: PERSISTIR bytes en una ruta. NO genera nombres de archivo.
/// El path completo (incluyendo nombre y extensión) lo decide el llamador
/// (típicamente <see cref="IFilePathBuilder"/>) y debe ser el mismo que se
/// persiste en <see cref="Models.Document.FilePath"/> para garantizar el
/// roundtrip DB ↔ disco.
/// </remarks>
public interface IFileStorageService
{
    /// <summary>
    /// Persiste el contenido de <paramref name="fileStream"/> en la ruta relativa
    /// indicada y devuelve la misma ruta como confirmación de la operación.
    /// </summary>
    /// <param name="fileStream">Stream con el contenido a persistir (se consume hasta el final).</param>
    /// <param name="relativePath">Ruta relativa al root del storage, INCLUYENDO el nombre de archivo y extensión.</param>
    /// <param name="ct">Token de cancelación.</param>
    Task<string> UploadAsync(Stream fileStream, string relativePath, CancellationToken ct = default);

    Task<Stream> DownloadAsync(string relativePath, CancellationToken ct = default);
    Task DeleteAsync(string relativePath, CancellationToken ct = default);
    Task<string> GetUrlAsync(string relativePath, TimeSpan expiration, CancellationToken ct = default);
}
