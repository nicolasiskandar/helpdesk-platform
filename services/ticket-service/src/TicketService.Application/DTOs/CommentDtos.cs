namespace TicketService.Application.DTOs;

public record AddCommentRequest(
    string Content,
    bool IsPrivate
);

public record CommentResponse(
    Guid Id,
    Guid AuthorUserId,
    string Content,
    bool IsPrivate,
    DateTime CreatedAt
);
