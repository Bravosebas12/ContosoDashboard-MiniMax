using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ContosoDashboard.Services.Documents;

/// <summary>
/// Servicio de auditoría. En lugar de escribir directamente en <c>ActivityLog</c>
/// (lo que causaba <c>InvalidOperationException</c> por concurrencia con el DbContext scoped),
/// enqueuea la entry a <see cref="IActivityLogQueue"/> para que el <see cref="ActivityLogBackgroundService"/>
/// la persista con su propio scope.
/// </summary>
public interface IActivityLogService
{
    /// <summary>
    /// Enqueuea un evento de auditoría. No bloquea — retorna inmediatamente tras enqueuear.
    /// La persistencia real ocurre en background.
    /// </summary>
    ValueTask LogAsync(
        string @event,
        int? documentId,
        int userId,
        string? ipAddress = null,
        object? metadata = null,
        CancellationToken ct = default);
}

public class ActivityLogService : IActivityLogService
{
    private readonly IActivityLogQueue _queue;
    private readonly ILogger<ActivityLogService> _logger;

    public ActivityLogService(IActivityLogQueue queue, ILogger<ActivityLogService> logger)
    {
        _queue = queue;
        _logger = logger;
    }

    public ValueTask LogAsync(
        string @event,
        int? documentId,
        int userId,
        string? ipAddress = null,
        object? metadata = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(@event))
            throw new ArgumentException("Event requerido.", nameof(@event));

        var entry = new ActivityLogEntry(
            Event: @event,
            DocumentId: documentId,
            UserId: userId,
            IpAddress: ipAddress,
            MetadataJson: metadata is null ? null : JsonSerializer.Serialize(metadata),
            Timestamp: DateTime.UtcNow);

        // Enqueue sin await del SaveChangesAsync (que ya no ocurre aquí).
        // Esto resuelve A1: el productor nunca toca el DbContext.
        return _queue.EnqueueAsync(entry, ct);
    }
}
