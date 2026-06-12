using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ContosoDashboard.Services.Documents;

/// <summary>
/// Implementación local de <see cref="IFileStorageService"/> que persiste en
/// filesystem local bajo <c>AppData/uploads/</c> (fuera de <c>wwwroot</c>).
/// Esta es la ÚNICA implementación de <see cref="IFileStorageService"/> en esta
/// release (ver FR-038 — sin SDKs cloud, sin mocks).
/// </summary>
public class LocalFileStorageService : IFileStorageService
{
    private readonly ILogger<LocalFileStorageService> _logger;
    private readonly string _rootPath;

    public LocalFileStorageService(ILogger<LocalFileStorageService> logger, string? rootPath = null)
    {
        _logger = logger;
        _rootPath = rootPath ?? Path.Combine(AppContext.BaseDirectory, "AppData", "uploads");
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<string> UploadAsync(Stream fileStream, string relativeDirectory, string fileExtension, CancellationToken ct = default)
    {
        if (fileStream == null) throw new ArgumentNullException(nameof(fileStream));
        if (string.IsNullOrWhiteSpace(relativeDirectory)) throw new ArgumentException("Directorio requerido.", nameof(relativeDirectory));
        if (string.IsNullOrWhiteSpace(fileExtension)) throw new ArgumentException("Extensión requerida.", nameof(fileExtension));

        var cleanExt = fileExtension.TrimStart('.').ToLowerInvariant();
        if (!DocumentConstants.AllowedMimeByExtension.ContainsKey(cleanExt))
            throw new ArgumentException($"Extensión no permitida: {cleanExt}", nameof(fileExtension));

        var guid = Guid.NewGuid().ToString("N");
        var relativePath = $"{relativeDirectory.Trim('/')}/{guid}.{cleanExt}";
        var fullPath = ResolveFullPath(relativePath);

        var fullDir = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("No se pudo determinar el directorio destino.");
        Directory.CreateDirectory(fullDir);

        await using var fs = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await fileStream.CopyToAsync(fs, ct).ConfigureAwait(false);

        _logger.LogInformation("Archivo persistido: {RelativePath} ({Bytes} bytes)", relativePath, fs.Length);
        return relativePath;
    }

    public Task<Stream> DownloadAsync(string relativePath, CancellationToken ct = default)
    {
        var fullPath = ResolveFullPath(relativePath);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Archivo no encontrado: {relativePath}", fullPath);

        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string relativePath, CancellationToken ct = default)
    {
        var fullPath = ResolveFullPath(relativePath);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            _logger.LogInformation("Archivo eliminado: {RelativePath}", relativePath);
        }
        return Task.CompletedTask;
    }

    public Task<string> GetUrlAsync(string relativePath, TimeSpan expiration, CancellationToken ct = default)
    {
        // En local no se firman URLs; se devuelve el path absoluto del filesystem.
        // En producción, una AzureBlobStorageService devolvería un SAS URL firmado.
        return Task.FromResult(ResolveFullPath(relativePath));
    }

    private string ResolveFullPath(string relativePath)
    {
        // Anti path-traversal: normalizar y verificar que el resultado está bajo _rootPath
        var combined = Path.GetFullPath(Path.Combine(_rootPath, relativePath));
        var rootFull = Path.GetFullPath(_rootPath) + Path.DirectorySeparatorChar;
        if (!combined.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException($"Path traversal detectado: {relativePath}");
        return combined;
    }
}
