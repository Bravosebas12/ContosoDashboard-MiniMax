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
        // T201: Persistir fuera de bin/ para que las subidas sobrevivan a `dotnet build`.
        // La convención es <ContentRoot>/AppData/uploads (junto al .csproj).
        var contentRoot = AppContext.BaseDirectory;
        // Subir dos niveles para salir de bin/Debug/net8.0 cuando se ejecuta desde `dotnet run`.
        if (contentRoot.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
        {
            contentRoot = Path.GetFullPath(Path.Combine(contentRoot, "..", "..", ".."));
        }
        _rootPath = rootPath ?? Path.Combine(contentRoot, "AppData", "uploads");
        Directory.CreateDirectory(_rootPath);
        _logger.LogInformation("LocalFileStorageService rootPath: {RootPath}", _rootPath);
    }

    public async Task<string> UploadAsync(Stream fileStream, string relativePath, CancellationToken ct = default)
    {
        if (fileStream == null) throw new ArgumentNullException(nameof(fileStream));
        if (string.IsNullOrWhiteSpace(relativePath)) throw new ArgumentException("Path is required.", nameof(relativePath));

        // El storage NO genera el nombre: el llamador (DocumentService +
        // FilePathBuilder) ya construyó el path completo. Aquí solo persistimos
        // bytes en la ruta indicada, garantizando roundtrip con Document.FilePath.
        var fullPath = ResolveFullPath(relativePath);

        var fullDir = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("Could not determine target directory.");
        Directory.CreateDirectory(fullDir);

        await using var fs = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await fileStream.CopyToAsync(fs, ct).ConfigureAwait(false);

        _logger.LogInformation("File persisted: {RelativePath} ({Bytes} bytes)", relativePath, fs.Length);
        return relativePath;
    }

    public Task<Stream> DownloadAsync(string relativePath, CancellationToken ct = default)
    {
        var fullPath = ResolveFullPath(relativePath);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"File not found: {relativePath}", fullPath);

        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string relativePath, CancellationToken ct = default)
    {
        var fullPath = ResolveFullPath(relativePath);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            _logger.LogInformation("File deleted: {RelativePath}", relativePath);
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
            throw new UnauthorizedAccessException($"Path traversal detected: {relativePath}");
        return combined;
    }
}
