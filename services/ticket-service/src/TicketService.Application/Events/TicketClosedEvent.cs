namespace TicketService.Application.Events;

public record TicketClosedEvent(
    Guid TicketId,
    string ReferenceNumber,
    Guid ClosedByUserId,
    DateTime ClosedAt,
    IReadOnlyList<Guid> AdminUserIds
);
