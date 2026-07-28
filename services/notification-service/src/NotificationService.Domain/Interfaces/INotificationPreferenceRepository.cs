using NotificationService.Domain.Entities;

namespace NotificationService.Domain.Interfaces;

public interface INotificationPreferenceRepository
{
    Task<NotificationPreference?> GetByUserIdAsync(Guid userId);
    Task<NotificationPreference> GetOrCreateByUserIdAsync(Guid userId);
    Task UpdateAsync(NotificationPreference preference);
}
