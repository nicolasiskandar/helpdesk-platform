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

    public async Task<IReadOnlyList<AgentWorkloadEntry>> GetAgentWorkloadAsync()
    {
        var openStatuses = new[] { 1, 2 }; // Open, In Progress
        var resolvedStatuses = new[] { 3, 4, 5 }; // Resolved-Pending, Closed, Resolved by AI

        var assignments = await _context.TicketAssignments
            .Include(a => a.Ticket)
                .ThenInclude(t => t.Category)
            .Include(a => a.Ticket)
                .ThenInclude(t => t.Priority)
            .Include(a => a.Ticket)
                .ThenInclude(t => t.Status)
            .Where(a =>
                (a.UnassignedAt == null && openStatuses.Contains(a.Ticket.StatusId)) ||
                resolvedStatuses.Contains(a.Ticket.StatusId))
            .AsNoTracking()
            .ToListAsync();

        var results = assignments
            .GroupBy(a => a.AgentUserId)
            .Select(g =>
            {
                var openTickets = g
                    .Where(a => a.UnassignedAt == null && openStatuses.Contains(a.Ticket.StatusId))
                    .OrderByDescending(a => a.Ticket.Priority.Level)
                    .ThenBy(a => a.Ticket.CreatedAt)
                    .Select(MapWorkloadTicket)
                    .ToList();

                var resolvedTickets = g
                    .Where(a => resolvedStatuses.Contains(a.Ticket.StatusId))
                    .GroupBy(a => a.TicketId)
                    .Select(ticketGroup => ticketGroup.OrderByDescending(a => a.AssignedAt).First())
                    .OrderByDescending(a => a.Ticket.UpdatedAt)
                    .Select(MapWorkloadTicket)
                    .ToList();

                return new AgentWorkloadEntry(
                    g.Key,
                    openTickets.Count,
                    resolvedTickets.Count,
                    openTickets,
                    resolvedTickets
                );
            })
            .OrderByDescending(w => w.OpenCount)
            .ThenByDescending(w => w.ResolvedCount)
            .ToList();

        return results;
    }

    private static AgentWorkloadTicketEntry MapWorkloadTicket(TicketAssignment assignment)
    {
        var ticket = assignment.Ticket;
        return new AgentWorkloadTicketEntry(
            ticket.Id,
            ticket.ReferenceNumber,
            ticket.Title,
            ticket.Category.Name,
            ticket.Priority.Name,
            ticket.Status.Name,
            ticket.CreatedAt,
            ticket.UpdatedAt
        );
    }
}
