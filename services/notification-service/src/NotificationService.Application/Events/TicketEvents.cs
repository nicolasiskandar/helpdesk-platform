namespace NotificationService.Application.Events;

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

public record TicketAssignedEvent(
    Guid TicketId,
    string ReferenceNumber,
    Guid AgentUserId,
    Guid AssignedByUserId,
    DateTime AssignedAt
);

public record TicketStatusChangedEvent(
    Guid TicketId,
    string ReferenceNumber,
    string OldStatus,
    string NewStatus,
    Guid ChangedByUserId,
    string ChangedByType,
    DateTime ChangedAt
);

public record TicketCommentedEvent(
    Guid TicketId,
    string ReferenceNumber,
    Guid AuthorUserId,
    string Content,
    bool IsPrivate,
    DateTime CreatedAt
);
