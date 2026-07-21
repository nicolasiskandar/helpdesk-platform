using TicketService.Domain.Entities;

namespace TicketService.Domain.Interfaces;

public interface ITicketAssignmentRepository
{
    Task<IReadOnlyList<TicketAssignment>> GetByTicketIdAsync(Guid ticketId);
    Task<TicketAssignment?> GetActiveAssignmentAsync(Guid ticketId, Guid agentUserId);
    Task AddAsync(TicketAssignment assignment);
    Task UpdateAsync(TicketAssignment assignment);
}
