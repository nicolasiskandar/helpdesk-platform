using NotificationService.Application.DTOs;

namespace NotificationService.Application.Interfaces;

public interface INotificationService
{
    Task<IReadOnlyList<NotificationResponse>> GetNotificationsAsync(Guid userId, int page, int pageSize, bool? unreadOnly = null);
    Task<int> GetCountAsync(Guid userId, bool? unreadOnly = null);
    Task<int> GetUnreadCountAsync(Guid userId);
    Task MarkAsReadAsync(Guid notificationId, Guid userId);
    Task MarkAllAsReadAsync(Guid userId);
    Task<PreferenceResponse> GetPreferencesAsync(Guid userId);
    Task UpdatePreferencesAsync(Guid userId, UpdatePreferenceRequest request);
    Task ProcessTicketEventAsync(string eventType, string payload);
}
