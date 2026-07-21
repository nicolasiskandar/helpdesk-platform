namespace TicketService.Application.Events;

public record TicketCreatedEvent(
    Guid TicketId,
    string ReferenceNumber,
    string Title,
    string Description,
    string CategoryName,
    string PriorityName,
    Guid CreatedByUserId,
    DateTime CreatedAt
);
