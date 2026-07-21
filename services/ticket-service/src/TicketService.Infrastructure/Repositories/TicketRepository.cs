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
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<Ticket?> GetByReferenceNumberAsync(string referenceNumber)
    {
        return await _context.Tickets
            .Include(t => t.Category)
            .Include(t => t.Priority)
            .Include(t => t.Status)
            .FirstOrDefaultAsync(t => t.ReferenceNumber == referenceNumber);
    }

    public async Task<IReadOnlyList<Ticket>> GetAllAsync(int page, int pageSize)
    {
        return await _context.Tickets
            .Include(t => t.Category)
            .Include(t => t.Priority)
            .Include(t => t.Status)
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Ticket>> GetByCreatedByUserIdAsync(Guid userId, int page, int pageSize)
    {
        return await _context.Tickets
            .Include(t => t.Category)
            .Include(t => t.Priority)
            .Include(t => t.Status)
            .Where(t => t.CreatedByUserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<int> GetCountAsync()
    {
        return await _context.Tickets.CountAsync();
    }

    public async Task<int> GetCountByCreatedByUserIdAsync(Guid userId)
    {
        return await _context.Tickets.CountAsync(t => t.CreatedByUserId == userId);
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
}
