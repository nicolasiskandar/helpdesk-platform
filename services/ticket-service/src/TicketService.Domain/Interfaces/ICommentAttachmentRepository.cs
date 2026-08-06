using TicketService.Domain.Entities;

namespace TicketService.Domain.Interfaces;

public interface ICommentAttachmentRepository
{
    Task<IReadOnlyList<CommentAttachment>> GetByTicketIdAsync(Guid ticketId);
    Task<IReadOnlyList<CommentAttachment>> GetByCommentIdAsync(Guid commentId);
    Task<CommentAttachment?> GetByIdAsync(Guid id);
    Task AddAsync(CommentAttachment attachment);
    Task DeleteAsync(CommentAttachment attachment);
}
