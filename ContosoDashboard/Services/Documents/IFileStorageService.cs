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
public interface IFileStorageService
{
    Task<string> UploadAsync(Stream fileStream, string relativeDirectory, string fileExtension, CancellationToken ct = default);
    Task<Stream> DownloadAsync(string relativePath, CancellationToken ct = default);
    Task DeleteAsync(string relativePath, CancellationToken ct = default);
    Task<string> GetUrlAsync(string relativePath, TimeSpan expiration, CancellationToken ct = default);
}
