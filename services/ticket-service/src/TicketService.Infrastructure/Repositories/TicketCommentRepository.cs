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

    public async Task<IReadOnlyList<TicketComment>> GetByTicketIdAsync(
        Guid ticketId,
        Guid viewerUserId,
        string viewerRole,
        Guid ticketCreatorUserId,
        HashSet<Guid> assignedAgentUserIds)
    {
        return await _context.TicketComments
            .Include(c => c.Recipients)
            .Where(c => c.TicketId == ticketId)
            .Where(c => !c.IsPrivate && !c.Recipients.Any()
                || viewerRole == "Admin"
                || c.AuthorUserId == viewerUserId
                || c.IsPrivate && !c.Recipients.Any()
                    && (viewerUserId == ticketCreatorUserId || assignedAgentUserIds.Contains(viewerUserId))
                || c.Recipients.Any(r => r.RecipientUserId == viewerUserId))
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();
    }

    public Task<TicketComment?> GetByIdAsync(Guid commentId)
    {
        return _context.TicketComments
            .Include(c => c.Recipients)
            .FirstOrDefaultAsync(c => c.Id == commentId);
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
