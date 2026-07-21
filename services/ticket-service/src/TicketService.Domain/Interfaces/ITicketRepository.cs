using TicketService.Domain.Entities;

namespace TicketService.Domain.Interfaces;

public interface ITicketRepository
{
    Task<Ticket?> GetByIdAsync(Guid id);
    Task<Ticket?> GetByReferenceNumberAsync(string referenceNumber);
    Task<IReadOnlyList<Ticket>> GetAllAsync(int page, int pageSize);
    Task<IReadOnlyList<Ticket>> GetByCreatedByUserIdAsync(Guid userId, int page, int pageSize);
    Task<int> GetCountAsync();
    Task<int> GetCountByCreatedByUserIdAsync(Guid userId);
    Task AddAsync(Ticket ticket);
    Task UpdateAsync(Ticket ticket);
}
