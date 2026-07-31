namespace TicketService.Application.Events;

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
