namespace TicketService.Application.DTOs;

public record AttachmentResponse(
    Guid Id,
    string FileName,
    string FileUrl,
    Guid UploadedByUserId,
    DateTime UploadedAt
);
