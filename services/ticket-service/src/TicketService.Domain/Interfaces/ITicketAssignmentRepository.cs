using TicketService.Domain.Entities;

namespace TicketService.Domain.Interfaces;

public interface ITicketAssignmentRepository
{
    Task<IReadOnlyList<TicketAssignment>> GetByTicketIdAsync(Guid ticketId);
    Task<TicketAssignment?> GetActiveAssignmentAsync(Guid ticketId, Guid agentUserId);
    Task<bool> HasActiveAssignmentAsync(Guid ticketId);
    Task AddAsync(TicketAssignment assignment);
    Task UpdateAsync(TicketAssignment assignment);
    Task DeleteAsync(TicketAssignment assignment);
    Task<IReadOnlyList<AgentWorkloadEntry>> GetAgentWorkloadAsync();
}
