namespace NotificationService.Domain.Entities;

public class ProcessedMessage
{
    public Guid Id { get; set; }
    public string MessageId { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}
