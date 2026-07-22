using TicketService.Domain.Entities;

namespace TicketService.Domain.Interfaces;

public interface IOutboxRepository
{
    Task AddAsync(OutboxMessage message);
    Task<IReadOnlyList<OutboxMessage>> GetUnprocessedMessagesAsync(int batchSize);
    Task MarkAsProcessedAsync(Guid id);
    Task MarkAsFailedAsync(Guid id, string error);
    Task MarkAsDLQAsync(Guid id, string error);
}
