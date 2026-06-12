using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ContosoDashboard.Models;

/// <summary>
/// Estado del escaneo antivirus aplicado a un documento.
/// </summary>
public enum DocumentAvStatus
{
    /// <summary>Archivo limpio — sin amenazas detectadas.</summary>
    Clean = 0,
    /// <summary>Archivo infectado — amenaza detectada (ver <see cref="Document.AvThreatName"/>).</summary>
    Infected = 1,
    /// <summary>Servicio de AV no estaba disponible (degraded mode, training).</summary>
    NotScanned = 2,
    /// <summary>Error al escanear (timeout, conexión, formato no soportado).</summary>
    Error = 3
}

/// <summary>
/// Representa un documento subido al sistema. PK entero por consistencia con
/// <see cref="User"/> y <see cref="Project"/> existentes.
/// Ver <c>specs/001-documents-management/data-model.md</c> para documentación completa.
/// </summary>
public class Document
{
    [Key]
    public int DocumentId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    /// <summary>
    /// Categoría enum-texto. Valores permitidos (validado en servicio):
    /// "Project Documents", "Team Resources", "Personal Files", "Reports", "Presentations", "Other".
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Path relativo al directorio de uploads. Formato: <c>{userId}/{projectIdOrPersonal}/{guid}.{ext}</c>.
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;

    public long FileSize { get; set; }

    [Required]
    [MaxLength(255)]
    public string FileType { get; set; } = string.Empty;

    /// <summary>
    /// Tags comma-separated, lowercase, max 5 tags × 50 chars.
    /// </summary>
    [MaxLength(500)]
    public string? Tags { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    [Required]
    public int UploadedByUserId { get; set; }

    public int? ProjectId { get; set; }

    public int? TaskId { get; set; }

    public DateTime? ReplacedAt { get; set; }

    public DocumentAvStatus AvScanStatus { get; set; } = DocumentAvStatus.Clean;

    public DateTime? AvScanAt { get; set; }

    [MaxLength(255)]
    public string? AvThreatName { get; set; }

    /// <summary>
    /// Nombre original del archivo (para descarga y display).
    /// Se diferencia de <see cref="FilePath"/> que usa GUID.
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string OriginalFileName { get; set; } = string.Empty;

    // Navigation properties
    [ForeignKey(nameof(UploadedByUserId))]
    public virtual User UploadedByUser { get; set; } = null!;

    [ForeignKey(nameof(ProjectId))]
    public virtual Project? Project { get; set; }

    public virtual ICollection<DocumentShare> DocumentShares { get; set; } = new List<DocumentShare>();
}
