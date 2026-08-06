using TicketService.Domain.Entities;
using TicketService.Domain.Interfaces;
using TicketService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace TicketService.Infrastructure.Repositories;

public class CommentAttachmentRepository : ICommentAttachmentRepository
{
    private readonly TicketDbContext _context;

    public CommentAttachmentRepository(TicketDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<CommentAttachment>> GetByTicketIdAsync(Guid ticketId)
    {
        var commentIds = await _context.TicketComments
            .Where(c => c.TicketId == ticketId)
            .Select(c => c.Id)
            .ToListAsync();

        return await _context.CommentAttachments
            .Where(a => commentIds.Contains(a.CommentId))
            .OrderByDescending(a => a.UploadedAt)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<CommentAttachment>> GetByCommentIdAsync(Guid commentId)
    {
        return await _context.CommentAttachments
            .Where(a => a.CommentId == commentId)
            .OrderByDescending(a => a.UploadedAt)
            .ToListAsync();
    }

    public async Task<CommentAttachment?> GetByIdAsync(Guid id)
    {
        return await _context.CommentAttachments
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task AddAsync(CommentAttachment attachment)
    {
        await _context.CommentAttachments.AddAsync(attachment);
    }

    public Task DeleteAsync(CommentAttachment attachment)
    {
        _context.CommentAttachments.Remove(attachment);
        return Task.CompletedTask;
    }
}
