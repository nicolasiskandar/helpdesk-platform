using TicketService.Domain.Entities;
using TicketService.Domain.Interfaces;
using TicketService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace TicketService.Infrastructure.Repositories;

public class TicketAuditLogRepository : ITicketAuditLogRepository
{
    private readonly TicketDbContext _context;

    public TicketAuditLogRepository(TicketDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<TicketAuditLogEntry>> GetByTicketIdAsync(Guid ticketId, int page, int pageSize)
    {
        return await _context.TicketAuditLogs
            .Where(a => a.TicketId == ticketId)
            .OrderByDescending(a => a.ChangedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<int> GetCountByTicketIdAsync(Guid ticketId)
    {
        return await _context.TicketAuditLogs.CountAsync(a => a.TicketId == ticketId);
    }

    public async Task AddAsync(TicketAuditLogEntry entry)
    {
        await _context.TicketAuditLogs.AddAsync(entry);
    }
}
