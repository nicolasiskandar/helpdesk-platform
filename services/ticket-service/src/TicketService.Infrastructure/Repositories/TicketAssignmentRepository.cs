using TicketService.Domain.Entities;
using TicketService.Domain.Interfaces;
using TicketService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace TicketService.Infrastructure.Repositories;

public class TicketAssignmentRepository : ITicketAssignmentRepository
{
    private readonly TicketDbContext _context;

    public TicketAssignmentRepository(TicketDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<TicketAssignment>> GetByTicketIdAsync(Guid ticketId)
    {
        return await _context.TicketAssignments
            .Where(a => a.TicketId == ticketId)
            .OrderByDescending(a => a.AssignedAt)
            .ToListAsync();
    }

    public async Task<TicketAssignment?> GetActiveAssignmentAsync(Guid ticketId, Guid agentUserId)
    {
        return await _context.TicketAssignments
            .FirstOrDefaultAsync(a => a.TicketId == ticketId && a.AgentUserId == agentUserId && a.UnassignedAt == null);
    }

    public async Task AddAsync(TicketAssignment assignment)
    {
        await _context.TicketAssignments.AddAsync(assignment);
    }

    public Task UpdateAsync(TicketAssignment assignment)
    {
        _context.TicketAssignments.Update(assignment);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(TicketAssignment assignment)
    {
        _context.TicketAssignments.Remove(assignment);
        return Task.CompletedTask;
    }
}
