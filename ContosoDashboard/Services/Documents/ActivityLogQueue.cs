using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using ContosoDashboard.Data;
using ContosoDashboard.Models;
using Microsoft.Extensions.Logging;

namespace ContosoDashboard.Services.Documents;

/// <summary>
/// Entry inmutable que se encola para persistencia asíncrona en <see cref="ActivityLog"/>.
/// Se usa como DTO entre productores (servicios del dominio) y el consumidor (<see cref="ActivityLogBackgroundService"/>).
/// </summary>
/// <param name="Event">Tipo de evento (e.g. <c>document.uploaded</c>).</param>
/// <param name="DocumentId">FK opcional al documento asociado.</param>
/// <param name="UserId">FK al usuario que ejecutó la acción.</param>
/// <param name="IpAddress">IPv4 o IPv6 del cliente (opcional).</param>
/// <param name="MetadataJson">Metadata serializada como JSON (opcional).</param>
/// <param name="Timestamp">Timestamp UTC del evento.</param>
public record ActivityLogEntry(
    string Event,
    int? DocumentId,
    int UserId,
    string? IpAddress,
    string? MetadataJson,
    DateTime Timestamp);

/// <summary>
/// Cola lock-free para <see cref="ActivityLogEntry"/>. Productores enqueuean (no bloquean),
/// el consumidor (<see cref="ActivityLogBackgroundService"/>) drena con su propio scope/DBContext.
/// Resuelve el bug A1 (InvalidOperationException por concurrencia en <c>ApplicationDbContext</c>).
/// </summary>
public interface IActivityLogQueue
{
    /// <summary>Enqueuea un entry para persistencia asíncrona. No bloquea.</summary>
    ValueTask EnqueueAsync(ActivityLogEntry entry, CancellationToken ct = default);

    /// <summary>Acceso de solo-lectura al canal (para el consumer BackgroundService).</summary>
    ChannelReader<ActivityLogEntry> Reader { get; }

    /// <summary>Marca el canal como completo para escritura (no más enqueues).</summary>
    void Complete();
}

/// <summary>
/// Implementación unbounded (sin backpressure). El logging estructurado es de bajo volumen
/// y la cardinalidad esperada (cientos de miles de entries/día) cabe en memoria sin problemas.
/// Para volúmenes extremos, cambiar a <see cref="Channel.CreateBounded{T}(int)"/>.
/// </summary>
public class ActivityLogQueue : IActivityLogQueue
{
    private readonly Channel<ActivityLogEntry> _channel = Channel.CreateUnbounded<ActivityLogEntry>(
        new UnboundedChannelOptions
        {
            SingleReader = true,    // Solo el BackgroundService consume
            SingleWriter = false,   // Múltiples servicios producen
            AllowSynchronousContinuations = false,
        });

    public ChannelReader<ActivityLogEntry> Reader => _channel.Reader;

    public ValueTask EnqueueAsync(ActivityLogEntry entry, CancellationToken ct = default)
    {
        if (entry is null) throw new ArgumentNullException(nameof(entry));
        return _channel.Writer.WriteAsync(entry, ct);
    }

    public void Complete() => _channel.Writer.TryComplete();
}
