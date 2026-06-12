using ContosoDashboard.Services.Documents;

namespace ContosoDashboard.Models;

/// <summary>
/// Mapping explícito de Document <-> DTOs públicos.
/// Evita AutoMapper por lineamientos de la constitución del proyecto.
/// </summary>
public static class DocumentMappingProfile
{
    public static DocumentDto ToDto(this Document document)
    {
        return new DocumentDto(
            document.DocumentId,
            document.Title,
            document.Description,
            document.Category,
            document.FilePath,
            document.FileSize,
            document.FileType,
            document.Tags,
            document.UploadedAt,
            document.UploadedByUserId,
            document.ProjectId,
            document.TaskId,
            document.ReplacedAt,
            document.AvScanStatus,
            document.AvThreatName,
            document.OriginalFileName);
    }

    public static Document FromUploadRequest(
        string title,
        string? description,
        string category,
        string filePath,
        long fileSize,
        string fileType,
        string? tags,
        int uploadedByUserId,
        int? projectId,
        int? taskId,
        string originalFileName,
        DocumentAvStatus avScanStatus,
        string? avThreatName)
    {
        return new Document
        {
            Title = title,
            Description = description,
            Category = category,
            FilePath = filePath,
            FileSize = fileSize,
            FileType = fileType,
            Tags = tags,
            UploadedByUserId = uploadedByUserId,
            ProjectId = projectId,
            TaskId = taskId,
            OriginalFileName = originalFileName,
            AvScanStatus = avScanStatus,
            AvThreatName = avThreatName,
            UploadedAt = DateTime.UtcNow,
            AvScanAt = DateTime.UtcNow,
        };
    }
}
