using System;
using System.Threading;
using System.Threading.Tasks;
using ContosoDashboard.Data;
using ContosoDashboard.Models;
using Microsoft.Extensions.Logging;

namespace ContosoDashboard.Services.Documents;

/// <summary>
/// Servicio para registrar eventos de auditoría en <see cref="ActivityLog"/>.
/// Usado por todos los servicios del dominio (DocumentService, DocumentShareService)
/// y por middleware de autorización (defense in depth — para loguear 403).
/// </summary>
public interface IActivityLogService
{
    Task LogAsync(
        string @event,
        int? documentId,
        int userId,
        string? ipAddress = null,
        object? metadata = null,
        CancellationToken ct = default);
}

public class ActivityLogService : IActivityLogService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<ActivityLogService> _logger;

    public ActivityLogService(ApplicationDbContext db, ILogger<ActivityLogService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task LogAsync(
        string @event,
        int? documentId,
        int userId,
        string? ipAddress = null,
        object? metadata = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(@event)) throw new ArgumentException("Event requerido.", nameof(@event));

        try
        {
            var entry = new ActivityLog
            {
                Event = @event,
                DocumentId = documentId,
                UserId = userId,
                IpAddress = ipAddress,
                Metadata = metadata is null ? null : System.Text.Json.JsonSerializer.Serialize(metadata),
                Timestamp = DateTime.UtcNow,
            };
            _db.ActivityLogs.Add(entry);
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Log estructurado aunque la DB falle — Constitución II.A09
            _logger.LogError(ex, "Fallo al registrar evento {Event} para usuario {UserId} (doc={DocumentId})",
                @event, userId, documentId);
        }
    }
}
