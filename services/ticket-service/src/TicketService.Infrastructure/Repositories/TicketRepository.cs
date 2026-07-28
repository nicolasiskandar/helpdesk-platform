using TicketService.Domain.Entities;
using TicketService.Domain.Interfaces;
using TicketService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace TicketService.Infrastructure.Repositories;

public class TicketRepository : ITicketRepository
{
    private readonly TicketDbContext _context;

    public TicketRepository(TicketDbContext context)
    {
        _context = context;
    }

    public async Task<Ticket?> GetByIdAsync(Guid id)
    {
        return await _context.Tickets
            .Include(t => t.Category)
            .Include(t => t.Priority)
            .Include(t => t.Status)
            .Include(t => t.Assignments)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<Ticket?> GetByReferenceNumberAsync(string referenceNumber)
    {
        return await _context.Tickets
            .Include(t => t.Category)
            .Include(t => t.Priority)
            .Include(t => t.Status)
            .Include(t => t.Assignments)
            .FirstOrDefaultAsync(t => t.ReferenceNumber == referenceNumber);
    }

    public async Task<IReadOnlyList<Ticket>> GetAllAsync(int page, int pageSize, DateTime? createdFrom = null, DateTime? createdTo = null)
    {
        var query = _context.Tickets
            .Include(t => t.Category)
            .Include(t => t.Priority)
            .Include(t => t.Status)
            .Include(t => t.Assignments)
            .AsQueryable();

        if (createdFrom.HasValue)
            query = query.Where(t => t.CreatedAt >= createdFrom.Value);
        if (createdTo.HasValue)
            query = query.Where(t => t.CreatedAt <= createdTo.Value);

        return await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Ticket>> GetByCreatedByUserIdAsync(Guid userId, int page, int pageSize, DateTime? createdFrom = null, DateTime? createdTo = null)
    {
        var closedStatus = await _context.Statuses.FirstOrDefaultAsync(s => s.Name == "Closed");

        var query = _context.Tickets
            .Include(t => t.Category)
            .Include(t => t.Priority)
            .Include(t => t.Status)
            .Include(t => t.Assignments)
            .Where(t => t.CreatedByUserId == userId
                || (closedStatus != null && t.StatusId == closedStatus.Id))
            .AsQueryable();

        if (createdFrom.HasValue)
            query = query.Where(t => t.CreatedAt >= createdFrom.Value);
        if (createdTo.HasValue)
            query = query.Where(t => t.CreatedAt <= createdTo.Value);

        return await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Ticket>> GetByAgentUserIdAsync(Guid agentUserId, int page, int pageSize, DateTime? createdFrom = null, DateTime? createdTo = null)
    {
        var openStatus = await _context.Statuses.FirstOrDefaultAsync(s => s.Name == "Open");
        var closedStatus = await _context.Statuses.FirstOrDefaultAsync(s => s.Name == "Closed");

        var query = _context.Tickets
            .Include(t => t.Category)
            .Include(t => t.Priority)
            .Include(t => t.Status)
            .Include(t => t.Assignments)
            .Where(t =>
                // Open tickets with no active assignment (available for pickup)
                (openStatus != null && t.StatusId == openStatus.Id &&
                    !_context.TicketAssignments.Any(a => a.TicketId == t.Id && a.UnassignedAt == null))
                // Tickets where this agent has an active assignment
                || _context.TicketAssignments.Any(a => a.TicketId == t.Id && a.AgentUserId == agentUserId && a.UnassignedAt == null)
                // Closed tickets visible to everyone
                || (closedStatus != null && t.StatusId == closedStatus.Id))
            .AsQueryable();

        if (createdFrom.HasValue)
            query = query.Where(t => t.CreatedAt >= createdFrom.Value);
        if (createdTo.HasValue)
            query = query.Where(t => t.CreatedAt <= createdTo.Value);

        return await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<int> GetCountAsync(DateTime? createdFrom = null, DateTime? createdTo = null)
    {
        var query = _context.Tickets.AsQueryable();

        if (createdFrom.HasValue)
            query = query.Where(t => t.CreatedAt >= createdFrom.Value);
        if (createdTo.HasValue)
            query = query.Where(t => t.CreatedAt <= createdTo.Value);

        return await query.CountAsync();
    }

    public async Task<int> GetCountByCreatedByUserIdAsync(Guid userId, DateTime? createdFrom = null, DateTime? createdTo = null)
    {
        var closedStatus = await _context.Statuses.FirstOrDefaultAsync(s => s.Name == "Closed");

        var query = _context.Tickets
            .Where(t => t.CreatedByUserId == userId
                || (closedStatus != null && t.StatusId == closedStatus.Id))
            .AsQueryable();

        if (createdFrom.HasValue)
            query = query.Where(t => t.CreatedAt >= createdFrom.Value);
        if (createdTo.HasValue)
            query = query.Where(t => t.CreatedAt <= createdTo.Value);

        return await query.CountAsync();
    }

    public async Task<int> GetCountByAgentUserIdAsync(Guid agentUserId, DateTime? createdFrom = null, DateTime? createdTo = null)
    {
        var openStatus = await _context.Statuses.FirstOrDefaultAsync(s => s.Name == "Open");
        var closedStatus = await _context.Statuses.FirstOrDefaultAsync(s => s.Name == "Closed");

        var query = _context.Tickets
            .Where(t =>
                (openStatus != null && t.StatusId == openStatus.Id &&
                    !_context.TicketAssignments.Any(a => a.TicketId == t.Id && a.UnassignedAt == null))
                || _context.TicketAssignments.Any(a => a.TicketId == t.Id && a.AgentUserId == agentUserId && a.UnassignedAt == null)
                || (closedStatus != null && t.StatusId == closedStatus.Id))
            .AsQueryable();

        if (createdFrom.HasValue)
            query = query.Where(t => t.CreatedAt >= createdFrom.Value);
        if (createdTo.HasValue)
            query = query.Where(t => t.CreatedAt <= createdTo.Value);

        return await query.CountAsync();
    }

    public async Task<IReadOnlyList<Ticket>> GetOpenUnassignedTicketsAsync(int page, int pageSize)
    {
        var openStatus = await _context.Statuses.FirstOrDefaultAsync(s => s.Name == "Open");

        return await _context.Tickets
            .Include(t => t.Category)
            .Include(t => t.Priority)
            .Include(t => t.Status)
            .Include(t => t.Assignments)
            .Where(t => openStatus != null && t.StatusId == openStatus.Id)
            .Where(t => !_context.TicketAssignments.Any(a => a.TicketId == t.Id && a.UnassignedAt == null))
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<int> GetOpenUnassignedTicketsCountAsync()
    {
        var openStatus = await _context.Statuses.FirstOrDefaultAsync(s => s.Name == "Open");

        return await _context.Tickets
            .Where(t => openStatus != null && t.StatusId == openStatus.Id)
            .Where(t => !_context.TicketAssignments.Any(a => a.TicketId == t.Id && a.UnassignedAt == null))
            .CountAsync();
    }

    public async Task AddAsync(Ticket ticket)
    {
        await _context.Tickets.AddAsync(ticket);
    }

    public Task UpdateAsync(Ticket ticket)
    {
        _context.Tickets.Update(ticket);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Ticket ticket)
    {
        _context.Tickets.Remove(ticket);
        return Task.CompletedTask;
    }
}
