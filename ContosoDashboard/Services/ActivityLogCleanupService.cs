using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ContosoDashboard.Data;
using ContosoDashboard.Services.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ContosoDashboard.Services.Documents;

/// <summary>
/// Servicio de limpieza de logs antiguos. Usado por <see cref="ActivityLogCleanupBackgroundService"/>
/// (T131 — per FR-031: retención mínima de 90 días).
/// </summary>
public interface IActivityLogCleanupService
{
    /// <summary>
    /// Elimina los ActivityLog con <c>Timestamp</c> anterior a la fecha de corte
    /// (<c>DateTime.UtcNow - retentionDays</c>). Retorna la cantidad de filas eliminadas.
    /// </summary>
    /// <param name="retentionDays">Días de retención (default 90, configurable).</param>
    Task<int> CleanupAsync(int retentionDays = DocumentConstants.LogRetentionDays, CancellationToken ct = default);
}

public class ActivityLogCleanupService : IActivityLogCleanupService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<ActivityLogCleanupService> _logger;

    public ActivityLogCleanupService(ApplicationDbContext db, ILogger<ActivityLogCleanupService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<int> CleanupAsync(int retentionDays = DocumentConstants.LogRetentionDays, CancellationToken ct = default)
    {
        if (retentionDays < 1) retentionDays = DocumentConstants.LogRetentionDays;

        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
        var deleted = await _db.ActivityLogs
            .Where(l => l.Timestamp < cutoff)
            .ExecuteDeleteAsync(ct);

        if (deleted > 0)
        {
            _logger.LogInformation(
                "ActivityLog cleanup: {Deleted} filas eliminadas (corte: {Cutoff:yyyy-MM-dd UTC}, retención: {Days} días)",
                deleted, cutoff, retentionDays);
        }
        else
        {
            _logger.LogDebug("ActivityLog cleanup: 0 filas eliminadas (corte: {Cutoff:yyyy-MM-dd UTC})", cutoff);
        }

        return deleted;
    }
}
