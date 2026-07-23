using TicketService.Domain.Entities;
using TicketService.Domain.Interfaces;
using TicketService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace TicketService.Infrastructure.Repositories;

public class TicketAttachmentRepository : ITicketAttachmentRepository
{
    private readonly TicketDbContext _context;

    public TicketAttachmentRepository(TicketDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<TicketAttachment>> GetByTicketIdAsync(Guid ticketId)
    {
        return await _context.TicketAttachments
            .Where(a => a.TicketId == ticketId)
            .OrderByDescending(a => a.UploadedAt)
            .ToListAsync();
    }

    public async Task AddAsync(TicketAttachment attachment)
    {
        await _context.TicketAttachments.AddAsync(attachment);
    }

    public Task DeleteAsync(TicketAttachment attachment)
    {
        _context.TicketAttachments.Remove(attachment);
        return Task.CompletedTask;
    }
}
