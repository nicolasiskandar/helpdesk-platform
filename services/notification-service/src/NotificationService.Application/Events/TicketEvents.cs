namespace NotificationService.Application.Events;

public record TicketCreatedEvent(
    Guid TicketId,
    string ReferenceNumber,
    string Title,
    string Description,
    string CategoryName,
    string PriorityName,
    Guid CreatedByUserId,
    DateTime CreatedAt,
    IReadOnlyList<Guid> ManagerUserIds,
    IReadOnlyList<Guid> AdminUserIds
);

public record TicketClosedEvent(
    Guid TicketId,
    string ReferenceNumber,
    Guid ClosedByUserId,
    DateTime ClosedAt,
    IReadOnlyList<Guid> AdminUserIds
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
    DateTime ChangedAt,
    IReadOnlyList<Guid> RecipientUserIds
);

public record TicketCommentedEvent(
    Guid TicketId,
    string ReferenceNumber,
    Guid AuthorUserId,
    string AuthorName,
    string Content,
    bool IsPrivate,
    Guid CommentId,
    Guid? ParentCommentId,
    IReadOnlyList<Guid> RecipientUserIds,
    DateTime CreatedAt
);
