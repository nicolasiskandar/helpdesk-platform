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
                // All open tickets
                (openStatus != null && t.StatusId == openStatus.Id)
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
                (openStatus != null && t.StatusId == openStatus.Id)
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

    public async Task<IReadOnlyList<Ticket>> GetForAnalyticsAsync(DateTime from, DateTime to)
    {
        return await _context.Tickets
            .AsNoTracking()
            .Where(t => t.CreatedAt >= from && t.CreatedAt < to)
            .Select(t => new Ticket
            {
                Id = t.Id,
                CreatedAt = t.CreatedAt,
                StatusId = t.StatusId,
                PriorityId = t.PriorityId
            })
            .ToListAsync();
    }

    public async Task<int> GetUnassignedCountAsync(DateTime from, DateTime to)
    {
        return await _context.Tickets
            .Where(t => t.CreatedAt >= from && t.CreatedAt < to)
            .Where(t => !_context.TicketAssignments.Any(a => a.TicketId == t.Id && a.UnassignedAt == null))
            .CountAsync();
    }

    public async Task<IReadOnlyList<Ticket>> GetClosedTicketsAsync(int page, int pageSize)
    {
        var closedStatus = await _context.Statuses.FirstOrDefaultAsync(s => s.Name == "Closed");

        return await _context.Tickets
            .Include(t => t.Category)
            .Include(t => t.Priority)
            .Include(t => t.Status)
            .Include(t => t.Assignments)
            .Where(t => closedStatus != null && t.StatusId == closedStatus.Id)
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<int> GetClosedTicketsCountAsync()
    {
        var closedStatus = await _context.Statuses.FirstOrDefaultAsync(s => s.Name == "Closed");

        return await _context.Tickets
            .Where(t => closedStatus != null && t.StatusId == closedStatus.Id)
            .CountAsync();
    }

    public async Task<Ticket?> GetRecentDuplicateAsync(Guid createdByUserId, string title, string description, int categoryId, TimeSpan window)
    {
        var since = DateTime.UtcNow.Subtract(window);

        return await _context.Tickets
            .Include(t => t.Category)
            .Include(t => t.Priority)
            .Include(t => t.Status)
            .Include(t => t.Assignments)
            .Where(t => t.CreatedByUserId == createdByUserId
                && t.CategoryId == categoryId
                && t.CreatedAt >= since
                && t.Title == title
                && t.Description == description)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync();
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

    public async Task<bool> TransitionStatusAsync(Guid ticketId, int fromStatusId, int toStatusId)
    {
        var affected = await _context.Tickets
            .Where(t => t.Id == ticketId && t.StatusId == fromStatusId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(t => t.StatusId, toStatusId)
                .SetProperty(t => t.UpdatedAt, DateTime.UtcNow));

        return affected > 0;
    }
}
