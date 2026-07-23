using TicketService.Domain.Entities;

namespace TicketService.Domain.Interfaces;

public interface ITicketCommentRepository
{
    Task<IReadOnlyList<TicketComment>> GetByTicketIdAsync(Guid ticketId, bool includeInternal);
    Task AddAsync(TicketComment comment);
    Task DeleteAsync(TicketComment comment);
}
