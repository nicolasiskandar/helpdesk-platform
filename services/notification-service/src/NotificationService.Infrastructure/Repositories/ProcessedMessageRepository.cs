using NotificationService.Domain.Entities;
using NotificationService.Domain.Interfaces;
using NotificationService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace NotificationService.Infrastructure.Repositories;

public class ProcessedMessageRepository : IProcessedMessageRepository
{
    private readonly NotificationDbContext _context;

    public ProcessedMessageRepository(NotificationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> ExistsAsync(string messageId)
    {
        return await _context.ProcessedMessages.AnyAsync(m => m.MessageId == messageId);
    }

    public async Task AddAsync(string messageId)
    {
        _context.ProcessedMessages.Add(new ProcessedMessage
        {
            Id = Guid.NewGuid(),
            MessageId = messageId,
            ProcessedAt = DateTime.UtcNow
        });

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Unique constraint on MessageId: a concurrent redelivery inserted
            // the row first. Treat as already-processed (idempotent).
        }
    }

    public async Task<int> DeleteOlderThanAsync(DateTime cutoff)
    {
        return await _context.ProcessedMessages
            .Where(m => m.ProcessedAt < cutoff)
            .ExecuteDeleteAsync();
    }
}
