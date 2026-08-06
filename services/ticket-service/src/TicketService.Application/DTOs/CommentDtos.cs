namespace TicketService.Application.DTOs;

public record AddCommentRequest(
    string Content,
    bool IsPrivate,
    Guid? ParentCommentId = null,
    IReadOnlyList<Guid>? RecipientUserIds = null
);

public record CommentFileUpload(
    string FileName,
    Stream Content,
    long Size
);

public record CommentAttachmentResponse(
    Guid Id,
    string FileName,
    string FileUrl,
    long Size,
    Guid UploadedByUserId,
    DateTime UploadedAt
);

public record CommentResponse(
    Guid Id,
    Guid AuthorUserId,
    string Content,
    bool IsPrivate,
    Guid? ParentCommentId,
    IReadOnlyList<Guid> RecipientUserIds,
    DateTime CreatedAt,
    IReadOnlyList<CommentAttachmentResponse>? Attachments = null
);
