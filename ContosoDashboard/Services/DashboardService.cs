using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ContosoDashboard.Data;
using ContosoDashboard.Models;
using ContosoDashboard.Services.Documents;

namespace ContosoDashboard.Services;

public interface IDashboardService
{
    Task<DashboardSummary> GetDashboardSummaryAsync(int userId);
    Task<List<Announcement>> GetActiveAnnouncementsAsync();

    /// <summary>
    /// Retorna los N documentos más recientes visibles para el usuario (propios + proyectos + shares).
    /// Usa <see cref="IMemoryCache"/> con TTL de 5 minutos (T121, T124).
    /// </summary>
    Task<List<RecentDocumentDto>> GetRecentDocumentsAsync(int userId, int count = 5);

    /// <summary>Conteo total de documentos visibles para el usuario (para cards de resumen, T123).</summary>
    Task<int> GetUserDocumentCountAsync(int userId);

    /// <summary>Invalida el cache del dashboard del usuario (se invoca tras un upload/delete, T124).</summary>
    void InvalidateUserDashboardAsync(int userId);
}

public class DashboardService : IDashboardService
{
    private readonly ApplicationDbContext _context;
    private readonly IMemoryCache _cache;

    // Cache keys
    private static string RecentDocsCacheKey(int userId, int count) => $"dashboard:user:{userId}:recentdocs:{count}";
    private static string SummaryCacheKey(int userId) => $"dashboard:user:{userId}:summary";
    private static string CountCacheKey(int userId) => $"dashboard:user:{userId}:count";

    private static readonly TimeSpan DashboardCacheTtl = TimeSpan.FromMinutes(5);

    public DashboardService(ApplicationDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<DashboardSummary> GetDashboardSummaryAsync(int userId)
    {
        // T124: cache de 5 min para evitar recálculo en cada navegación
        return await _cache.GetOrCreateAsync(SummaryCacheKey(userId), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = DashboardCacheTtl;

            var now = DateTime.UtcNow;

            var summary = new DashboardSummary
            {
                TotalActiveTasks = await _context.Tasks
                    .CountAsync(t => t.AssignedUserId == userId && t.Status != Models.TaskStatus.Completed),

                TasksDueToday = await _context.Tasks
                    .CountAsync(t => t.AssignedUserId == userId
                        && t.DueDate.HasValue
                        && t.DueDate.Value.Date == now.Date
                        && t.Status != Models.TaskStatus.Completed),

                ActiveProjects = await _context.Projects
                    .Where(p => p.ProjectManagerId == userId || p.ProjectMembers.Any(pm => pm.UserId == userId))
                    .Where(p => p.Status == ProjectStatus.Active)
                    .CountAsync(),

                UnreadNotifications = await _context.Notifications
                    .CountAsync(n => n.UserId == userId && !n.IsRead),

                TotalDocuments = await GetUserDocumentCountAsync(userId),
            };

            return summary;
        }) ?? new DashboardSummary();
    }

    public async Task<List<Announcement>> GetActiveAnnouncementsAsync()
    {
        var now = DateTime.UtcNow;

        return await _context.Announcements
            .Include(a => a.CreatedByUser)
            .Where(a => a.IsActive
                && a.PublishDate <= now
                && (!a.ExpiryDate.HasValue || a.ExpiryDate.Value > now))
            .OrderByDescending(a => a.PublishDate)
            .Take(5)
            .ToListAsync();
    }

    public async Task<List<RecentDocumentDto>> GetRecentDocumentsAsync(int userId, int count = 5)
    {
        if (count < 1) count = 1;
        if (count > 25) count = 25;

        return await _cache.GetOrCreateAsync(RecentDocsCacheKey(userId, count), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = DashboardCacheTtl;

            // Mismo criterio de visibilidad que IDocumentService.ListAsync:
            // propios, de proyectos del usuario, o compartidos activos.
            var myProjectIds = _context.ProjectMembers
                .Where(pm => pm.UserId == userId)
                .Select(pm => pm.ProjectId);
            var mySharedDocIds = _context.DocumentShares
                .Where(s => s.SharedWithUserId == userId && s.RevokedAt == null
                    && (s.ExpiresAt == null || s.ExpiresAt > DateTime.UtcNow))
                .Select(s => s.DocumentId);

            var docs = await _context.Documents.AsNoTracking()
                .Where(d => d.UploadedByUserId == userId
                    || (d.ProjectId != null && myProjectIds.Contains(d.ProjectId.Value))
                    || mySharedDocIds.Contains(d.DocumentId))
                .OrderByDescending(d => d.UploadedAt)
                .Take(count)
                .Select(d => new RecentDocumentDto(
                    d.DocumentId,
                    d.Title,
                    d.Category,
                    d.FileType,
                    d.FileSize,
                    d.UploadedAt,
                    d.UploadedByUserId))
                .ToListAsync();

            return docs;
        }) ?? new List<RecentDocumentDto>();
    }

    public async Task<int> GetUserDocumentCountAsync(int userId)
    {
        // El conteo de documentos también puede ser costoso con 10k+ docs, así que lo cacheamos.
        return await _cache.GetOrCreateAsync(CountCacheKey(userId), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = DashboardCacheTtl;

            var myProjectIds = _context.ProjectMembers
                .Where(pm => pm.UserId == userId)
                .Select(pm => pm.ProjectId);
            var mySharedDocIds = _context.DocumentShares
                .Where(s => s.SharedWithUserId == userId && s.RevokedAt == null
                    && (s.ExpiresAt == null || s.ExpiresAt > DateTime.UtcNow))
                .Select(s => s.DocumentId);

            return await _context.Documents.AsNoTracking()
                .Where(d => d.UploadedByUserId == userId
                    || (d.ProjectId != null && myProjectIds.Contains(d.ProjectId.Value))
                    || mySharedDocIds.Contains(d.DocumentId))
                .CountAsync();
        });
    }

    public void InvalidateUserDashboardAsync(int userId)
    {
        // T124: tras un upload/delete, se invalidan todas las claves cacheadas del dashboard del usuario.
        // El próximo render consultará DB y repoblará el cache.
        _cache.Remove(SummaryCacheKey(userId));
        _cache.Remove(CountCacheKey(userId));
        for (int c = 1; c <= 25; c++)
        {
            _cache.Remove(RecentDocsCacheKey(userId, c));
        }
    }
}

/// <summary>
/// Resumen del dashboard del usuario. Ver <see cref="IDashboardService.GetDashboardSummaryAsync"/>.
/// </summary>
public class DashboardSummary
{
    public int TotalActiveTasks { get; set; }
    public int TasksDueToday { get; set; }
    public int ActiveProjects { get; set; }
    public int UnreadNotifications { get; set; }
    /// <summary>Total de documentos visibles para el usuario (T123).</summary>
    public int TotalDocuments { get; set; }
}

/// <summary>DTO ligero para el widget de "Documentos Recientes" del dashboard (T120).</summary>
public record RecentDocumentDto(
    int DocumentId,
    string Title,
    string Category,
    string FileType,
    long FileSize,
    DateTime UploadedAt,
    int UploadedByUserId);
