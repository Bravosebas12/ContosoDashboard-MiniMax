using System.Collections.Generic;
using System.Linq;

namespace ContosoDashboard.Services.Documents;

/// <summary>
/// Constantes del dominio de documentos: categorías permitidas, MIME whitelist,
/// límites. Ver <c>specs/001-documents-management/data-model.md</c> §Document.
/// </summary>
public static class DocumentConstants
{
    // Categorías (enum-texto, per stakeholder doc)
    public const string CategoryProjectDocuments = "Project Documents";
    public const string CategoryTeamResources = "Team Resources";
    public const string CategoryPersonalFiles = "Personal Files";
    public const string CategoryReports = "Reports";
    public const string CategoryPresentations = "Presentations";
    public const string CategoryOther = "Other";

    public static readonly IReadOnlyList<string> AllowedCategories = new[]
    {
        CategoryProjectDocuments,
        CategoryTeamResources,
        CategoryPersonalFiles,
        CategoryReports,
        CategoryPresentations,
        CategoryOther,
    };

    // MIME types whitelist (16 tipos per StakeholderDoc §9.3)
    public const string MimePdf = "application/pdf";
    public const string MimeDoc = "application/msword";
    public const string MimeDocx = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    public const string MimeXls = "application/vnd.ms-excel";
    public const string MimeXlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    public const string MimePpt = "application/vnd.ms-powerpoint";
    public const string MimePptx = "application/vnd.openxmlformats-officedocument.presentationml.presentation";
    public const string MimeTxt = "text/plain";
    public const string MimeJpeg = "image/jpeg";
    public const string MimePng = "image/png";

    public static readonly IReadOnlyDictionary<string, string> AllowedMimeByExtension =
        new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["pdf"] = MimePdf,
            ["doc"] = MimeDoc,
            ["docx"] = MimeDocx,
            ["xls"] = MimeXls,
            ["xlsx"] = MimeXlsx,
            ["ppt"] = MimePpt,
            ["pptx"] = MimePptx,
            ["txt"] = MimeTxt,
            ["jpg"] = MimeJpeg,
            ["jpeg"] = MimeJpeg,
            ["png"] = MimePng,
        };

    public static readonly IReadOnlyList<string> PreviewableMimeTypes = new[]
    {
        MimePdf,
        MimeJpeg,
        MimePng,
    };

    // Límites
    public const long MaxFileSizeBytes = 25 * 1024 * 1024; // 25 MB (per FR-002)
    public const long MaxPreviewSizeBytes = 10 * 1024 * 1024; // 10 MB (per FR-019)
    public const int MaxTitleLength = 200;
    public const int MaxDescriptionLength = 2000;
    public const int MaxTags = 5;
    public const int MaxTagLength = 50;
    public const int MaxFileTypeLength = 255;
    public const int MaxFilePathLength = 500;
    public const int LogRetentionDays = 90; // per FR-031

    /// <summary>Valida que una extensión y un MIME type sean consistentes con la whitelist.</summary>
    public static bool IsAllowed(string extension, string mimeType)
    {
        if (string.IsNullOrWhiteSpace(extension) || string.IsNullOrWhiteSpace(mimeType))
            return false;
        var cleanExt = extension.TrimStart('.').ToLowerInvariant();
        return AllowedMimeByExtension.TryGetValue(cleanExt, out var allowedMime)
               && string.Equals(allowedMime, mimeType, System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Indica si el MIME type es visualizable inline (PDF, JPEG, PNG).</summary>
    public static bool IsPreviewable(string mimeType)
        => PreviewableMimeTypes.Any(m => string.Equals(m, mimeType, System.StringComparison.OrdinalIgnoreCase));
}
