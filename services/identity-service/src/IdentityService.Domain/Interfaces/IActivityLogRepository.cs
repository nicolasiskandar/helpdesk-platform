using IdentityService.Domain.Entities;

namespace IdentityService.Domain.Interfaces;

public interface IActivityLogRepository
{
    Task AddAsync(UserActivityLog log);
}
