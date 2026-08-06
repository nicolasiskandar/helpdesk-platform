using NotificationService.Domain.Interfaces;
using NotificationService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace NotificationService.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly NotificationDbContext _context;

    public UnitOfWork(NotificationDbContext context,
        INotificationRepository notifications,
        INotificationPreferenceRepository notificationPreferences,
        IProcessedMessageRepository processedMessages)
    {
        _context = context;
        Notifications = notifications;
        NotificationPreferences = notificationPreferences;
        ProcessedMessages = processedMessages;
    }

    public INotificationRepository Notifications { get; }
    public INotificationPreferenceRepository NotificationPreferences { get; }
    public IProcessedMessageRepository ProcessedMessages { get; }

    public async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
