namespace TicketService.Domain.Entities;

public class TicketAssignment
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public Guid AgentUserId { get; set; }
    public Guid AssignedByUserId { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UnassignedAt { get; set; }

    public Ticket Ticket { get; set; } = null!;
}
