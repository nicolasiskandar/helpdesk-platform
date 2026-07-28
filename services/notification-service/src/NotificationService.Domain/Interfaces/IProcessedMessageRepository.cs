namespace NotificationService.Domain.Interfaces;

public interface IProcessedMessageRepository
{
    Task<bool> ExistsAsync(string messageId);
    Task AddAsync(string messageId);
}
