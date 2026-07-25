using IdentityService.Domain.Entities;

namespace IdentityService.Domain.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id);
    Task<User?> GetByEmailAsync(string email);
    Task<bool> EmailExistsAsync(string email);
    Task<bool> HasAdminAsync();
    Task<IReadOnlyList<User>> GetAllAsync(string? search, int? roleId, bool? isActive, int page, int pageSize);
    Task<int> GetCountAsync(string? search, int? roleId, bool? isActive);
    Task AddAsync(User user);
    Task UpdateAsync(User user);
    Task DeleteAsync(User user);
}
