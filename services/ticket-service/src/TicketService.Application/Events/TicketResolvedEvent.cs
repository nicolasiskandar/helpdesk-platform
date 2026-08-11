namespace TicketService.Application.Events;

public record TicketResolvedEvent(
    Guid TicketId,
    string ReferenceNumber,
    string Title,
    string Description,
    Guid ResolvedByUserId,
    IReadOnlyList<Guid> RemainingAssigneeIds,
    DateTime ResolvedAt,
    string? CategoryName,
    string? PriorityName,
    string ResolvedStatusName
);
