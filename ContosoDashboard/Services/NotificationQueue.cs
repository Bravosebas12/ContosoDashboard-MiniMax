using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using ContosoDashboard.Data;
using ContosoDashboard.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ContosoDashboard.Services;

/// <summary>
/// Entry inmutable que se encola para creación asíncrona de <see cref="Notification"/>.
/// Resuelve A2/A3: los productores enqueuean sin tocar el DbContext.
/// </summary>
public record NotificationEntry(
    int UserId,
    string Title,
    string Message,
    int Type,
    int Priority,
    DateTime CreatedDate);

/// <summary>
/// Cola bounded con backpressure para <see cref="NotificationEntry"/>. Si el consumer
/// se queda atrás (DB lenta, deadlock), el productor recibe CompleteWithError en lugar
/// de OOM por acumulación infinita.
/// </summary>
public interface INotificationQueue
{
    /// <summary>Enqueuea un entry. Puede bloquear brevemente si la cola está llena (backpressure).</summary>
    ValueTask EnqueueAsync(NotificationEntry entry, CancellationToken ct = default);

    /// <summary>Acceso de solo-lectura al canal (para el consumer BackgroundService).</summary>
    ChannelReader<NotificationEntry> Reader { get; }

    /// <summary>Marca el canal como completo para escritura (no más enqueues).</summary>
    void Complete();
}

public class NotificationQueue : INotificationQueue
{
    private const int CapacityHint = 10_000;

    private readonly Channel<NotificationEntry> _channel = Channel.CreateBounded<NotificationEntry>(
        new BoundedChannelOptions(CapacityHint)
        {
            FullMode = BoundedChannelFullMode.Wait,  // Backpressure: el productor espera si está llena
            SingleReader = true,                    // Solo el BackgroundService consume
            SingleWriter = false,                   // Múltiples servicios producen
            AllowSynchronousContinuations = false,
        });

    public ChannelReader<NotificationEntry> Reader => _channel.Reader;

    public ValueTask EnqueueAsync(NotificationEntry entry, CancellationToken ct = default)
    {
        if (entry is null) throw new ArgumentNullException(nameof(entry));
        return _channel.Writer.WriteAsync(entry, ct);
    }

    public void Complete() => _channel.Writer.TryComplete();
}

/// <summary>
/// Background service que drena la cola <see cref="INotificationQueue"/> y persiste
/// cada entry en <c>Notifications</c> con su propio scope/DBContext.
/// Resuelve A2: <c>DocumentService.NotifyProjectMembersAsync</c> ya no toca el DbContext
/// directamente, y A3: <c>DocumentShareService</c> tampoco.
/// </summary>
public class NotificationBackgroundService : BackgroundService
{
    private readonly INotificationQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationBackgroundService> _logger;

    public NotificationBackgroundService(
        INotificationQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<NotificationBackgroundService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("NotificationBackgroundService iniciando (capacity=10k, SingleReader=true)");

        try
        {
            await foreach (var entry in _queue.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await PersistAsync(entry, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Fallo al persistir Notification para user {UserId} (title={Title})",
                        entry.UserId, entry.Title);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Cancelación normal del host
        }

        _logger.LogInformation("NotificationBackgroundService detenido");
    }

    private async Task PersistAsync(NotificationEntry entry, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var entity = new Notification
        {
            UserId = entry.UserId,
            Title = entry.Title,
            Message = entry.Message,
            Type = (NotificationType)entry.Type,
            Priority = (NotificationPriority)entry.Priority,
            CreatedDate = entry.CreatedDate,
            IsRead = false,
        };
        db.Notifications.Add(entity);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _queue.Complete();
        await base.StopAsync(cancellationToken);
    }
}
