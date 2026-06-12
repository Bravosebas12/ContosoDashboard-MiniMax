using System;
using System.Text.RegularExpressions;

namespace ContosoDashboard.Services.Documents;

/// <summary>
/// Genera y valida paths seguros para archivos subidos.
/// Patrón: <c>{userId}/{projectIdOrPersonal}/{guid}.{ext}</c>.
/// Anti path-traversal: normaliza y verifica que el path resultante es seguro.
/// </summary>
public interface IFilePathBuilder
{
    /// <summary>Genera un path relativo seguro para un archivo nuevo.</summary>
    /// <param name="userId">ID del usuario uploader (se sanitiza).</param>
    /// <param name="projectId">ID del proyecto (opcional; null = "personal").</param>
    /// <param name="fileExtension">Extensión SIN el punto.</param>
    string BuildPath(string userId, int? projectId, string fileExtension);

    /// <summary>Valida que un path cumple con el patrón seguro.</summary>
    bool IsValidPath(string relativePath);
}

public class FilePathBuilder : IFilePathBuilder
{
    // Regex: {segmento}/{segmento_o_personal}/{guid}.{ext}
    private static readonly Regex PathPattern = new(
        @"^[\w\-]+/(?:[\w\-]+|personal)/[a-f0-9]{32}\.[a-z0-9]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public string BuildPath(string userId, int? projectId, string fileExtension)
    {
        if (string.IsNullOrWhiteSpace(userId)) throw new ArgumentException("userId requerido.", nameof(userId));
        if (string.IsNullOrWhiteSpace(fileExtension)) throw new ArgumentException("Extensión requerida.", nameof(fileExtension));

        var cleanUserId = SanitizeSegment(userId);
        var segment = projectId.HasValue ? projectId.Value.ToString() : "personal";
        var cleanExt = fileExtension.TrimStart('.').ToLowerInvariant();
        var guid = Guid.NewGuid().ToString("N");
        return $"{cleanUserId}/{segment}/{guid}.{cleanExt}";
    }

    public bool IsValidPath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return false;
        if (relativePath.Contains("..", StringComparison.Ordinal)) return false; // anti path-traversal
        return PathPattern.IsMatch(relativePath);
    }

    private static string SanitizeSegment(string segment)
    {
        // Permitir solo letras, dígitos, guiones y underscores; max 50 chars
        var cleaned = Regex.Replace(segment, @"[^\w\-]", "");
        if (cleaned.Length > 50) cleaned = cleaned.Substring(0, 50);
        if (string.IsNullOrEmpty(cleaned)) throw new ArgumentException("Segmento inválido tras sanitización.", nameof(segment));
        return cleaned;
    }
}
