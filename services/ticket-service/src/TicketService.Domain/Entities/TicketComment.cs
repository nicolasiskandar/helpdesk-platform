namespace TicketService.Domain.Entities;

public class TicketComment
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public Guid AuthorUserId { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool IsPrivate { get; set; }
    public Guid? ParentCommentId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Ticket Ticket { get; set; } = null!;
    public TicketComment? ParentComment { get; set; }
    public ICollection<TicketComment> Replies { get; set; } = new List<TicketComment>();
    public ICollection<TicketCommentRecipient> Recipients { get; set; } = new List<TicketCommentRecipient>();
}
