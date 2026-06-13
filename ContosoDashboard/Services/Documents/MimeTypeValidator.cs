using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.StaticFiles;

namespace ContosoDashboard.Services.Documents;

/// <summary>
/// Valida que el MIME type reportado por el cliente (header) sea consistente con
/// los magic bytes del contenido del archivo. Esto mitiga A03 (MIME spoofing —
/// CHK008 del checklist de seguridad).
/// </summary>
public interface IMimeTypeValidator
{
    /// <summary>
    /// Lee los primeros bytes del stream y compara el signature (magic bytes) con
    /// la extensión declarada. Devuelve el MIME type detectado (o null si no se reconoce).
    /// Lanza <see cref="InvalidDataException"/> si el signature no coincide con
    /// la extensión declarada.
    /// </summary>
    Task<string?> ValidateAndDetectAsync(Stream fileStream, string declaredExtension, CancellationToken ct = default);
}

public class MimeTypeValidator : IMimeTypeValidator
{
    private readonly FileExtensionContentTypeProvider _provider = new();

    // Magic bytes (primeros 4-8 bytes) por extensión
    private static readonly (string Ext, byte[] Magic, int Offset)[] Signatures =
    {
        ("pdf",  new byte[] { 0x25, 0x50, 0x44, 0x46 }, 0), // %PDF
        ("png",  new byte[] { 0x89, 0x50, 0x4E, 0x47 }, 0), // ‰PNG
        ("jpg",  new byte[] { 0xFF, 0xD8, 0xFF }, 0),          // JPEG
        ("zip",  new byte[] { 0x50, 0x4B, 0x03, 0x04 }, 0),   // PK.. (docx, xlsx, pptx son ZIPs)
    };

    public async Task<string?> ValidateAndDetectAsync(Stream fileStream, string declaredExtension, CancellationToken ct = default)
    {
        if (fileStream == null) throw new ArgumentNullException(nameof(fileStream));
        if (string.IsNullOrWhiteSpace(declaredExtension)) throw new ArgumentException("File extension is required.", nameof(declaredExtension));

        var cleanExt = declaredExtension.TrimStart('.').ToLowerInvariant();
        if (!DocumentConstants.AllowedMimeByExtension.ContainsKey(cleanExt))
            throw new InvalidDataException($"File extension not allowed: {cleanExt}");

        // Guardar posición original del stream
        var originalPosition = fileStream.CanSeek ? fileStream.Position : 0;

        // Leer primeros 8 bytes (suficiente para todos los signatures definidos)
        var buffer = new byte[8];
        var bytesRead = 0;
        if (fileStream.CanSeek)
        {
            fileStream.Position = 0;
            bytesRead = await fileStream.ReadAsync(buffer.AsMemory(0, 8), ct).ConfigureAwait(false);
            fileStream.Position = originalPosition;
        }
        else
        {
            bytesRead = await fileStream.ReadAsync(buffer.AsMemory(0, 8), ct).ConfigureAwait(false);
        }

        // Buscar signature
        foreach (var (ext, magic, offset) in Signatures)
        {
            if (bytesRead < magic.Length + offset) continue;
            if (buffer.Skip(offset).Take(magic.Length).SequenceEqual(magic))
            {
                // Si la extensión declarada es de tipo ZIP (docx, xlsx, pptx), el signature ZIP es válido
                if (cleanExt == "docx" || cleanExt == "xlsx" || cleanExt == "pptx")
                {
                    if (ext == "zip") return DocumentConstants.AllowedMimeByExtension[cleanExt];
                }
                else if (cleanExt == ext)
                {
                    return DocumentConstants.AllowedMimeByExtension[cleanExt];
                }
                else
                {
                    throw new InvalidDataException(
                        $"The file signature ({ext}) does not match the declared extension ({cleanExt}). Possible spoofing.");
                }
            }
        }

        // Sin signature reconocible: confiar en Content-Type (txt, doc, xls, ppt son legacy)
        // Devolver el MIME esperado para la extensión declarada
        return DocumentConstants.AllowedMimeByExtension.TryGetValue(cleanExt, out var mime) ? mime : null;
    }
}
