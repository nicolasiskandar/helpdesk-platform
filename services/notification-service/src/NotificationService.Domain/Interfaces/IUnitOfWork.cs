using NotificationService.Domain.Interfaces;

namespace NotificationService.Domain.Interfaces;

public interface IUnitOfWork : IDisposable
{
    INotificationRepository Notifications { get; }
    INotificationPreferenceRepository NotificationPreferences { get; }
    IProcessedMessageRepository ProcessedMessages { get; }
    Task<int> SaveChangesAsync();
}
