using System;
using System.Threading;
using System.Threading.Tasks;
using ContosoDashboard.Data;
using ContosoDashboard.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ContosoDashboard.Services.Documents;

/// <summary>
/// Background service que drena la cola <see cref="IActivityLogQueue"/> y persiste
/// cada entry en <see cref="ActivityLog"/> con su propio scope y <see cref="ApplicationDbContext"/>.
/// Resuelve A1, A3, A5: el productor nunca toca el DbContext directamente.
/// </summary>
public class ActivityLogBackgroundService : BackgroundService
{
    private readonly IActivityLogQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ActivityLogBackgroundService> _logger;

    public ActivityLogBackgroundService(
        IActivityLogQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<ActivityLogBackgroundService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ActivityLogBackgroundService iniciando (SingleReader=true)");

        try
        {
            // ReadAllAsync cancela limpiamente cuando stoppingToken se dispara
            // o cuando el channel se completa (escritor llamó Complete()).
            await foreach (var entry in _queue.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await PersistAsync(entry, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw; // Propaga cancelación del host
                }
                catch (Exception ex)
                {
                    // Try/catch interno: un fallo de persistencia NO debe tumbar el consumer
                    // (A5 — resiliencia). Log estructurado y continúa con la siguiente entry.
                    _logger.LogError(ex,
                        "Fallo al persistir ActivityLog entry {Event} (user={UserId}, doc={DocumentId})",
                        entry.Event, entry.UserId, entry.DocumentId);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Cancelación normal del host — sale del bucle
        }

        _logger.LogInformation("ActivityLogBackgroundService detenido");
    }

    private async Task PersistAsync(ActivityLogEntry entry, CancellationToken ct)
    {
        // Scope por entry: garantiza que ApplicationDbContext es una instancia fresca
        // (NUNCA compartida con el productor).
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var entity = new ActivityLog
        {
            Event = entry.Event,
            DocumentId = entry.DocumentId,
            UserId = entry.UserId,
            IpAddress = entry.IpAddress,
            Metadata = entry.MetadataJson,
            Timestamp = entry.Timestamp,
        };
        db.ActivityLogs.Add(entity);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        // Al detener el host, dejamos de aceptar nuevas entries pero drenamos las pendientes
        _queue.Complete();
        await base.StopAsync(cancellationToken);
    }
}
