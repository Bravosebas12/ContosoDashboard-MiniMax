using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ContosoDashboard.Data;
using ContosoDashboard.Models;
using Microsoft.EntityFrameworkCore;

namespace ContosoDashboard.Services;

public interface INotificationService
{
    Task<List<Notification>> GetUserNotificationsAsync(int userId, bool unreadOnly = false);

    /// <summary>
    /// Enqueuea una notificación para persistencia asíncrona.
    /// No bloquea — retorna inmediatamente tras enqueuear.
    /// </summary>
    ValueTask EnqueueAsync(Notification notification, CancellationToken ct = default);

    /// <summary>
    /// DEPRECADO: usa <see cref="EnqueueAsync"/> en su lugar. Mantenido para compatibilidad
    /// con código que verificó el contrato original con <c>await CreateNotificationAsync</c>.
    /// Internamente enqueuea y retorna sin esperar la persistencia.
    /// </summary>
    [Obsolete("Use EnqueueAsync. CreateNotificationAsync se mantiene solo para compatibilidad legacy.")]
    Task<Notification> CreateNotificationAsync(Notification notification);

    Task<bool> MarkAsReadAsync(int notificationId, int requestingUserId);
    Task<int> GetUnreadCountAsync(int userId);
}

public class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _context;
    private readonly INotificationQueue _queue;

    public NotificationService(ApplicationDbContext context, INotificationQueue queue)
    {
        _context = context;
        _queue = queue;
    }

    public async Task<List<Notification>> GetUserNotificationsAsync(int userId, bool unreadOnly = false)
    {
        var query = _context.Notifications
            .Where(n => n.UserId == userId);

        if (unreadOnly)
            query = query.Where(n => !n.IsRead);

        return await query
            .OrderByDescending(n => n.Priority)
            .ThenByDescending(n => n.CreatedDate)
            .Take(50)
            .ToListAsync();
    }

    public ValueTask EnqueueAsync(Notification notification, CancellationToken ct = default)
    {
        if (notification is null) throw new ArgumentNullException(nameof(notification));

        var entry = new NotificationEntry(
            UserId: notification.UserId,
            Title: notification.Title ?? string.Empty,
            Message: notification.Message ?? string.Empty,
            Type: (int)notification.Type,
            Priority: (int)notification.Priority,
            CreatedDate: notification.CreatedDate == default ? DateTime.UtcNow : notification.CreatedDate);

        // Enqueue: el productor nunca toca el DbContext (resuelve A2).
        return _queue.EnqueueAsync(entry, ct);
    }

    [Obsolete("Use EnqueueAsync.")]
    public async Task<Notification> CreateNotificationAsync(Notification notification)
    {
        // Compatibilidad: enqueuea y retorna el objeto sin esperar la persistencia.
        await EnqueueAsync(notification);
        return notification;
    }

    public async Task<bool> MarkAsReadAsync(int notificationId, int requestingUserId)
    {
        var notification = await _context.Notifications.FindAsync(notificationId);
        if (notification == null) return false;

        // Authorization: Users can only mark their own notifications as read
        if (notification.UserId != requestingUserId)
        {
            return false;
        }

        notification.IsRead = true;
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<int> GetUnreadCountAsync(int userId)
    {
        return await _context.Notifications
            .CountAsync(n => n.UserId == userId && !n.IsRead);
    }
}
