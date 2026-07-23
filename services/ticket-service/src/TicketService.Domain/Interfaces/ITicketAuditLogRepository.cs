using TicketService.Domain.Entities;

namespace TicketService.Domain.Interfaces;

public interface ITicketAuditLogRepository
{
    Task<IReadOnlyList<TicketAuditLogEntry>> GetByTicketIdAsync(Guid ticketId, int page, int pageSize);
    Task<int> GetCountByTicketIdAsync(Guid ticketId);
    Task AddAsync(TicketAuditLogEntry entry);
    Task DeleteAsync(TicketAuditLogEntry entry);
}
