namespace NotificationService.Domain.Entities;

public class NotificationPreference
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public bool TicketCreatedInApp { get; set; } = true;
    public bool TicketCreatedEmail { get; set; } = true;
    public bool TicketAssignedInApp { get; set; } = true;
    public bool TicketAssignedEmail { get; set; } = true;
    public bool TicketUnassignedInApp { get; set; } = true;
    public bool TicketUnassignedEmail { get; set; } = true;
    public bool TicketStatusChangedInApp { get; set; } = true;
    public bool TicketStatusChangedEmail { get; set; } = true;
    public bool TicketCommentedInApp { get; set; } = true;
    public bool TicketCommentedEmail { get; set; } = true;
}
