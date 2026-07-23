using TicketService.Domain.Entities;
using TicketService.Domain.Interfaces;
using TicketService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace TicketService.Infrastructure.Repositories;

public class TicketCommentRepository : ITicketCommentRepository
{
    private readonly TicketDbContext _context;

    public TicketCommentRepository(TicketDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<TicketComment>> GetByTicketIdAsync(Guid ticketId, bool includeInternal)
    {
        var query = _context.TicketComments.Where(c => c.TicketId == ticketId);

        if (!includeInternal)
        {
            query = query.Where(c => !c.IsInternal);
        }

        return await query.OrderBy(c => c.CreatedAt).ToListAsync();
    }

    public async Task AddAsync(TicketComment comment)
    {
        await _context.TicketComments.AddAsync(comment);
    }

    public Task DeleteAsync(TicketComment comment)
    {
        _context.TicketComments.Remove(comment);
        return Task.CompletedTask;
    }
}
