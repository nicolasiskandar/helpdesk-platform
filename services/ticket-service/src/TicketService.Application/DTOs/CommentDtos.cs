namespace TicketService.Application.DTOs;

public record AddCommentRequest(
    string Content,
    bool IsInternal
);

public record CommentResponse(
    Guid Id,
    Guid AuthorUserId,
    string Content,
    bool IsInternal,
    DateTime CreatedAt
);
