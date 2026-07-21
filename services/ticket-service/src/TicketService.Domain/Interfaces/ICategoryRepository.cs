using TicketService.Domain.Entities;

namespace TicketService.Domain.Interfaces;

public interface ICategoryRepository
{
    Task<Category?> GetByIdAsync(int id);
    Task<IReadOnlyList<Category>> GetAllAsync();
}
