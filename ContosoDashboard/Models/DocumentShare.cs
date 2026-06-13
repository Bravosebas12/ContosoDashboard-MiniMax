using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ContosoDashboard.Models;

/// <summary>
/// Nivel de permiso para un share.
/// </summary>
public enum DocumentSharePermission
{
    /// <summary>Solo lectura (default).</summary>
    Read = 0,
    /// <summary>Lectura y escritura (futuro, no implementado en esta release).</summary>
    Write = 1
}

/// <summary>
/// Representa un permiso de acceso compartido a un documento.
/// Reglas de negocio (per Clarifications Q1):
/// - El dueño (uploader) puede compartir con cualquier usuario de la organización.
/// - Un Project Manager solo puede compartir dentro de su proyecto.
/// - Otros roles NO pueden compartir.
/// </summary>
public class DocumentShare
{
    [Key]
    public int DocumentShareId { get; set; }

    [Required]
    public int DocumentId { get; set; }

    public int? SharedWithUserId { get; set; }

    [MaxLength(50)]
    public string? SharedWithRole { get; set; }

    public DocumentSharePermission Permission { get; set; } = DocumentSharePermission.Read;

    public DateTime SharedAt { get; set; } = DateTime.UtcNow;

    [Required]
    public int SharedByUserId { get; set; }

    public DateTime? ExpiresAt { get; set; }

    /// <summary>Soft delete — preserva la fila para auditoría.</summary>
    public DateTime? RevokedAt { get; set; }

    public int? RevokedByUserId { get; set; }

    // Navigation properties
    [ForeignKey(nameof(DocumentId))]
    public virtual Document Document { get; set; } = null!;

    [ForeignKey(nameof(SharedWithUserId))]
    public virtual User? SharedWithUser { get; set; }

    [ForeignKey(nameof(SharedByUserId))]
    public virtual User SharedByUser { get; set; } = null!;
}
