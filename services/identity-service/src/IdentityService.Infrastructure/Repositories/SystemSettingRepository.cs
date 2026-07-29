using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using IdentityService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Infrastructure.Repositories;

public class SystemSettingRepository : ISystemSettingRepository
{
    private readonly IdentityDbContext _context;

    public SystemSettingRepository(IdentityDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<SystemSetting>> GetAllAsync()
    {
        return await _context.SystemSettings
            .OrderBy(s => s.Id)
            .ToListAsync();
    }

    public async Task<SystemSetting?> GetByKeyAsync(string key)
    {
        return await _context.SystemSettings
            .FirstOrDefaultAsync(s => s.Key == key);
    }

    public Task UpdateRangeAsync(IEnumerable<SystemSetting> settings)
    {
        _context.SystemSettings.UpdateRange(settings);
        return Task.CompletedTask;
    }
}
