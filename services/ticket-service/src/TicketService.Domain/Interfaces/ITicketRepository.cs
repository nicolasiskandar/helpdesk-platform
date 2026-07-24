using TicketService.Domain.Entities;

namespace TicketService.Domain.Interfaces;

public interface ITicketRepository
{
    Task<Ticket?> GetByIdAsync(Guid id);
    Task<Ticket?> GetByReferenceNumberAsync(string referenceNumber);
    Task<IReadOnlyList<Ticket>> GetAllAsync(int page, int pageSize, DateTime? createdFrom = null, DateTime? createdTo = null);
    Task<IReadOnlyList<Ticket>> GetByCreatedByUserIdAsync(Guid userId, int page, int pageSize, DateTime? createdFrom = null, DateTime? createdTo = null);
    Task<int> GetCountAsync(DateTime? createdFrom = null, DateTime? createdTo = null);
    Task<int> GetCountByCreatedByUserIdAsync(Guid userId, DateTime? createdFrom = null, DateTime? createdTo = null);
    Task AddAsync(Ticket ticket);
    Task UpdateAsync(Ticket ticket);
    Task DeleteAsync(Ticket ticket);
}
