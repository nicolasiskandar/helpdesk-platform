using IdentityService.Domain.Entities;

namespace IdentityService.Domain.Interfaces;

public interface IRoleRepository
{
    Task<Role?> GetByIdAsync(int id);
    Task<Role?> GetByNameAsync(string name);
    Task<IReadOnlyList<Role>> GetAllAsync();
}
