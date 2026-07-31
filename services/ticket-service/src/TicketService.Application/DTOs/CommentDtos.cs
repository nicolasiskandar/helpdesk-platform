namespace TicketService.Application.DTOs;

public record AddCommentRequest(
    string Content,
    bool IsPrivate,
    Guid? ParentCommentId = null,
    IReadOnlyList<Guid>? RecipientUserIds = null
);

public record CommentResponse(
    Guid Id,
    Guid AuthorUserId,
    string Content,
    bool IsPrivate,
    Guid? ParentCommentId,
    IReadOnlyList<Guid> RecipientUserIds,
    DateTime CreatedAt
);
