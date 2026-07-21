namespace TicketService.Application.Events;

public record TicketStatusChangedEvent(
    Guid TicketId,
    string ReferenceNumber,
    string OldStatus,
    string NewStatus,
    Guid ChangedByUserId,
    string ChangedByType,
    DateTime ChangedAt
);
