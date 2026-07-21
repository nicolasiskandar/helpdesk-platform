using TicketService.Domain.Entities;
using TicketService.Domain.Interfaces;
using TicketService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace TicketService.Infrastructure.Repositories;

public class PriorityRepository : IPriorityRepository
{
    private readonly TicketDbContext _context;

    public PriorityRepository(TicketDbContext context)
    {
        _context = context;
    }

    public async Task<Priority?> GetByIdAsync(int id)
    {
        return await _context.Priorities.FindAsync(id);
    }

    public async Task<IReadOnlyList<Priority>> GetAllAsync()
    {
        return await _context.Priorities.OrderBy(p => p.Level).ToListAsync();
    }
}
