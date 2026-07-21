using TicketService.Domain.Entities;

namespace TicketService.Domain.Interfaces;

public interface IStatusRepository
{
    Task<Status?> GetByIdAsync(int id);
    Task<Status?> GetByNameAsync(string name);
    Task<IReadOnlyList<Status>> GetAllAsync();
}
