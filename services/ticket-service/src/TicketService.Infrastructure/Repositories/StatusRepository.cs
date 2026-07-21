using TicketService.Domain.Entities;
using TicketService.Domain.Interfaces;
using TicketService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace TicketService.Infrastructure.Repositories;

public class StatusRepository : IStatusRepository
{
    private readonly TicketDbContext _context;

    public StatusRepository(TicketDbContext context)
    {
        _context = context;
    }

    public async Task<Status?> GetByIdAsync(int id)
    {
        return await _context.Statuses.FindAsync(id);
    }

    public async Task<Status?> GetByNameAsync(string name)
    {
        return await _context.Statuses.FirstOrDefaultAsync(s => s.Name == name);
    }

    public async Task<IReadOnlyList<Status>> GetAllAsync()
    {
        return await _context.Statuses.OrderBy(s => s.Id).ToListAsync();
    }
}
