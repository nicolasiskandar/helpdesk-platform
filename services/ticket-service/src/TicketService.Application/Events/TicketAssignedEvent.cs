namespace TicketService.Application.Events;

public record TicketAssignedEvent(
    Guid TicketId,
    string ReferenceNumber,
    Guid AgentUserId,
    Guid AssignedByUserId,
    DateTime AssignedAt
);
