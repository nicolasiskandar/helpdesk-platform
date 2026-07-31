namespace TicketService.Domain.Entities;

public class TicketCommentRecipient
{
    public Guid CommentId { get; set; }
    public Guid RecipientUserId { get; set; }

    public TicketComment Comment { get; set; } = null!;
}
