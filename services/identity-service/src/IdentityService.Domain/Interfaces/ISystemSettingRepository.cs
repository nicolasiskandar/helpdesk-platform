using IdentityService.Domain.Entities;

namespace IdentityService.Domain.Interfaces;

public interface ISystemSettingRepository
{
    Task<IReadOnlyList<SystemSetting>> GetAllAsync();
    Task<SystemSetting?> GetByKeyAsync(string key);
    Task UpdateRangeAsync(IEnumerable<SystemSetting> settings);
}
