namespace TicketService.Application.Events;

public record TicketResolvedEvent(
    Guid TicketId,
    string ReferenceNumber,
    Guid ResolvedByUserId,
    IReadOnlyList<Guid> RemainingAssigneeIds,
    DateTime ResolvedAt
);
