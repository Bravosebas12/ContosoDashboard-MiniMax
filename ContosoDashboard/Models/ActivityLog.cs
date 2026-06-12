using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ContosoDashboard.Models;

/// <summary>
/// Tipo de evento de auditoría registrado en <see cref="ActivityLog"/>.
/// </summary>
public static class ActivityLogEvents
{
    public const string DocumentUploaded = "document.uploaded";
    public const string DocumentDownloaded = "document.downloaded";
    public const string DocumentDeleted = "document.deleted";
    public const string DocumentReplaced = "document.replaced";
    public const string DocumentShared = "document.shared";
    public const string DocumentRevoked = "document.revoked";
    public const string DocumentAccessDenied = "document.access_denied";
    public const string DocumentScanned = "document.scanned";
}

/// <summary>
/// Registro de auditoría de eventos relacionados con documentos.
/// Separado de <see cref="Notification"/> (que es para comunicación al usuario);
/// aquí el propósito es trazabilidad (per FR-029, FR-031).
/// Retención mínima: 90 días (per FR-031).
/// </summary>
public class ActivityLog
{
    [Key]
    public long ActivityLogId { get; set; }

    [Required]
    [MaxLength(50)]
    public string Event { get; set; } = string.Empty;

    public int? DocumentId { get; set; }

    [Required]
    public int UserId { get; set; }

    /// <summary>IPv4 (15) o IPv6 (45 chars max).</summary>
    [MaxLength(45)]
    public string? IpAddress { get; set; }

    /// <summary>JSON con datos extra (result, fileSize, mimeType, etc.).</summary>
    [MaxLength(2000)]
    public string? Metadata { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey(nameof(DocumentId))]
    public virtual Document? Document { get; set; }

    [ForeignKey(nameof(UserId))]
    public virtual User User { get; set; } = null!;
}
