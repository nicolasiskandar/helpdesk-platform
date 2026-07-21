using TicketService.Domain.Entities;

namespace TicketService.Domain.Interfaces;

public interface IPriorityRepository
{
    Task<Priority?> GetByIdAsync(int id);
    Task<IReadOnlyList<Priority>> GetAllAsync();
}
