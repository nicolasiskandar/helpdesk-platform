using TicketService.Domain.Entities;

namespace TicketService.Domain.Interfaces;

public interface ITicketCommentRepository
{
    Task<IReadOnlyList<TicketComment>> GetByTicketIdAsync(Guid ticketId, Guid viewerUserId, string viewerRole, Guid ticketCreatorUserId, HashSet<Guid> assignedAgentUserIds);
    Task<TicketComment?> GetByIdAsync(Guid commentId);
    Task AddAsync(TicketComment comment);
    Task DeleteAsync(TicketComment comment);
}
