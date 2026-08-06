namespace TicketService.Domain.Entities;

public class CommentAttachment
{
    public Guid Id { get; set; }
    public Guid CommentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public Guid UploadedByUserId { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public long Size { get; set; }

    public TicketComment Comment { get; set; } = null!;
}
