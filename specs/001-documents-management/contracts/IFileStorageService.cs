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
    /// <summary>
    /// Sube un archivo al backend de almacenamiento. Devuelve el path relativo
    /// donde se guardó (formato: <c>{userId}/{projectIdOrPersonal}/{guid}.{ext}</c>).
    /// </summary>
    /// <param name="fileStream">Contenido del archivo. El stream se lee completo
    /// desde la posición actual hasta el final; el caller es responsable de
    /// posicionarlo correctamente.</param>
    /// <param name="relativeDirectory">Directorio relativo (ej. <c>{userId}/personal</c>
    /// o <c>{userId}/5</c> para proyecto 5). NO incluir el nombre del archivo.</param>
    /// <param name="fileExtension">Extensión SIN el punto (ej. <c>"pdf"</c>, <c>"docx"</c>).
    /// Se valida contra una whitelist interna.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Path relativo completo donde se persistió el archivo.</returns>
    /// <exception cref="ArgumentException">Si la extensión no está en la whitelist.</exception>
    /// <exception cref="InvalidOperationException">Si el path generado ya existe (colisión de GUID — debe ser imposible).</exception>
    /// <exception cref="OperationCanceledException">Si la operación se cancela.</exception>
    Task<string> UploadAsync(Stream fileStream, string relativeDirectory, string fileExtension, CancellationToken ct = default);

    /// <summary>
    /// Lee el contenido de un archivo como Stream. El caller es responsable
    /// de disposear el stream retornado.
    /// </summary>
    /// <param name="relativePath">Path relativo devuelto por <see cref="UploadAsync"/>.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Stream posicionado en 0, listo para leer.</returns>
    /// <exception cref="FileNotFoundException">Si el archivo no existe en el backend.</exception>
    Task<Stream> DownloadAsync(string relativePath, CancellationToken ct = default);

    /// <summary>
    /// Elimina un archivo del backend. No-op si el archivo no existe (idempotente).
    /// </summary>
    /// <param name="relativePath">Path relativo devuelto por <see cref="UploadAsync"/>.</param>
    /// <param name="ct">Token de cancelación.</param>
    Task DeleteAsync(string relativePath, CancellationToken ct = default);

    /// <summary>
    /// Devuelve una URL firmada o path absoluto para acceder al archivo. En esta
    /// release con <see cref="LocalFileStorageService"/> devuelve el path absoluto
    /// en el filesystem local (uso interno, NO expuesta al cliente — el cliente
    /// siempre accede vía endpoint autenticado del DocumentService).
    /// </summary>
    /// <param name="relativePath">Path relativo del archivo.</param>
    /// <param name="expiration">Tiempo de expiración del URL (en esta release, ignorado).</param>
    /// <returns>URL o path absoluto para acceder al archivo.</returns>
    Task<string> GetUrlAsync(string relativePath, TimeSpan expiration, CancellationToken ct = default);
}
