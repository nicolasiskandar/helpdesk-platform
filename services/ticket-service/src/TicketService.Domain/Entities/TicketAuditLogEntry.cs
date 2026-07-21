namespace TicketService.Domain.Entities;

public class TicketAuditLogEntry
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public Guid ChangedByUserId { get; set; }
    public string ChangedByType { get; set; } = string.Empty;
    public string FieldChanged { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

    public Ticket Ticket { get; set; } = null!;
}
