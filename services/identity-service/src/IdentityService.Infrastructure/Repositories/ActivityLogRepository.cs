using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using IdentityService.Infrastructure.Data;

namespace IdentityService.Infrastructure.Repositories;

public class ActivityLogRepository : IActivityLogRepository
{
    private readonly IdentityDbContext _context;

    public ActivityLogRepository(IdentityDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(UserActivityLog log)
    {
        await _context.UserActivityLogs.AddAsync(log);
    }
}
