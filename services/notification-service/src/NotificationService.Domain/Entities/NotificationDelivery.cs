namespace NotificationService.Domain.Entities;

public class NotificationDelivery
{
    public Guid Id { get; set; }
    public Guid NotificationId { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public DateTime? SentAt { get; set; }
    public string? Error { get; set; }

    public Notification Notification { get; set; } = null!;
}
