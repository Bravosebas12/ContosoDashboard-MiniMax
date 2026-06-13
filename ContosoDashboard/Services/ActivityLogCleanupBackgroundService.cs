using System;
using System.Threading;
using System.Threading.Tasks;
using ContosoDashboard.Services.Documents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ContosoDashboard.Services;

/// <summary>
/// Background service que ejecuta <see cref="IActivityLogCleanupService.CleanupAsync"/>
/// una vez al día (T131 — per FR-031).
///
/// <para>Cadencia: cada 24h (configurable vía <see cref="CleanupInterval"/>).</para>
/// <para>Retención: 90 días (per FR-031, configurable vía <see cref="DocumentConstants.LogRetentionDays"/>).</para>
/// </summary>
public class ActivityLogCleanupBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ActivityLogCleanupBackgroundService> _logger;

    /// <summary>Intervalo entre ejecuciones (default 24h).</summary>
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromHours(24);

    /// <summary>Retraso inicial para evitar ejecutar al arranque de la app (default 60s).</summary>
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(60);

    public ActivityLogCleanupBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<ActivityLogCleanupBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "ActivityLogCleanupBackgroundService iniciando (intervalo: {Interval}, retraso inicial: {Delay})",
            CleanupInterval, InitialDelay);

        try
        {
            await Task.Delay(InitialDelay, stoppingToken);
        }
        catch (TaskCanceledException)
        {
            return; // Servicio detenido durante el retraso inicial
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var cleanup = scope.ServiceProvider.GetRequiredService<IActivityLogCleanupService>();
                var deleted = await cleanup.CleanupAsync(ct: stoppingToken);
                _logger.LogInformation("Limpieza periódica completada: {Deleted} logs eliminados", deleted);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // No dejamos que una excepción tumbe el background service — se reintenta en la próxima iteración.
                _logger.LogError(ex, "Error durante limpieza de ActivityLog — se reintentará en la próxima iteración");
            }

            try
            {
                await Task.Delay(CleanupInterval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("ActivityLogCleanupBackgroundService detenido");
    }
}
