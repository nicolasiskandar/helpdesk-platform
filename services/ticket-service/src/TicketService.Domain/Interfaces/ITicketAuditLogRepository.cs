using TicketService.Domain.Entities;

namespace TicketService.Domain.Interfaces;

public interface ITicketAuditLogRepository
{
    Task<IReadOnlyList<TicketAuditLogEntry>> GetByTicketIdAsync(Guid ticketId, int page, int pageSize);
    Task<IReadOnlyList<TicketAuditLogEntry>> GetStatusTransitionsAsync(Guid ticketId);
    Task<IReadOnlyList<TicketAuditLogEntry>> GetResolutionTransitionsAsync(DateTime from, DateTime to);
    Task<int> GetCountByTicketIdAsync(Guid ticketId);
    Task AddAsync(TicketAuditLogEntry entry);
    Task DeleteAsync(TicketAuditLogEntry entry);
}
