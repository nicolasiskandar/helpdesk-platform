using TicketService.Domain.Entities;
using TicketService.Domain.Interfaces;
using TicketService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace TicketService.Infrastructure.Repositories;

public class OutboxRepository : IOutboxRepository
{
    private readonly TicketDbContext _context;

    public OutboxRepository(TicketDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(OutboxMessage message)
    {
        await _context.OutboxMessages.AddAsync(message);
    }

    public async Task<IReadOnlyList<OutboxMessage>> GetUnprocessedMessagesAsync(int batchSize)
    {
        return await _context.OutboxMessages
            .Where(m => m.ProcessedAt == null && m.RetryCount < m.MaxRetries)
            .OrderBy(m => m.CreatedAt)
            .Take(batchSize)
            .ToListAsync();
    }

    public async Task MarkAsProcessedAsync(Guid id)
    {
        var message = await _context.OutboxMessages.FindAsync(id);
        if (message != null)
        {
            message.ProcessedAt = DateTime.UtcNow;
            _context.OutboxMessages.Update(message);
        }
    }

    public async Task MarkAsFailedAsync(Guid id, string error)
    {
        var message = await _context.OutboxMessages.FindAsync(id);
        if (message != null)
        {
            message.RetryCount++;
            message.Error = error;
            _context.OutboxMessages.Update(message);
        }
    }

    public async Task MarkAsDLQAsync(Guid id, string error)
    {
        var message = await _context.OutboxMessages.FindAsync(id);
        if (message != null)
        {
            message.ProcessedAt = DateTime.UtcNow;
            message.Error = $"[DLQ] {error}";
            _context.OutboxMessages.Update(message);
        }
    }
}
