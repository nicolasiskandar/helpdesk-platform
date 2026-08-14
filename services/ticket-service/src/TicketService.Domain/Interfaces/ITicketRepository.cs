using TicketService.Domain.Entities;

namespace TicketService.Domain.Interfaces;

public interface ITicketRepository
{
    Task<Ticket?> GetByIdAsync(Guid id);
    Task<Ticket?> GetByReferenceNumberAsync(string referenceNumber);
    Task<IReadOnlyList<Ticket>> GetAllAsync(int page, int pageSize, DateTime? createdFrom = null, DateTime? createdTo = null);
    Task<IReadOnlyList<Ticket>> GetByCreatedByUserIdAsync(Guid userId, int page, int pageSize, DateTime? createdFrom = null, DateTime? createdTo = null);
    Task<IReadOnlyList<Ticket>> GetByAgentUserIdAsync(Guid agentUserId, int page, int pageSize, DateTime? createdFrom = null, DateTime? createdTo = null);
    Task<int> GetCountAsync(DateTime? createdFrom = null, DateTime? createdTo = null);
    Task<int> GetCountByCreatedByUserIdAsync(Guid userId, DateTime? createdFrom = null, DateTime? createdTo = null);
    Task<int> GetCountByAgentUserIdAsync(Guid agentUserId, DateTime? createdFrom = null, DateTime? createdTo = null);
    Task<IReadOnlyList<Ticket>> GetOpenUnassignedTicketsAsync(int page, int pageSize);
    Task<int> GetOpenUnassignedTicketsCountAsync();
    Task<IReadOnlyList<Ticket>> GetForAnalyticsAsync(DateTime from, DateTime to);
    Task<int> GetUnassignedCountAsync(DateTime from, DateTime to);
    Task<IReadOnlyList<Ticket>> GetClosedTicketsAsync(int page, int pageSize);
    Task<int> GetClosedTicketsCountAsync();

    /// <summary>
    /// Returns the most recent ticket created by <paramref name="createdByUserId"/>
    /// within <paramref name="window"/> that matches the same title, description, and
    /// category — used to deduplicate accidental double-submits of the create form
    /// (idempotent create). Null when no match exists.
    /// </summary>
    Task<Ticket?> GetRecentDuplicateAsync(Guid createdByUserId, string title, string description, int categoryId, TimeSpan window);

    Task AddAsync(Ticket ticket);
    Task UpdateAsync(Ticket ticket);
    Task DeleteAsync(Ticket ticket);

    /// <summary>
    /// Atomically flips a ticket's status from <paramref name="fromStatusId"/> to
    /// <paramref name="toStatusId"/> in a single UPDATE. Returns false when the row
    /// no longer matches (someone else transitioned it first). Used to make
    /// concurrent ticket claims race-safe.
    /// </summary>
    Task<bool> TransitionStatusAsync(Guid ticketId, int fromStatusId, int toStatusId);
}
