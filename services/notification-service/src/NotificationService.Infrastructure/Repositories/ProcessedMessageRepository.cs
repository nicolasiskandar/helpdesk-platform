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
        await _context.SaveChangesAsync();
    }
}
