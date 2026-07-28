using NotificationService.Domain.Entities;
using NotificationService.Domain.Interfaces;
using NotificationService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace NotificationService.Infrastructure.Repositories;

public class NotificationPreferenceRepository : INotificationPreferenceRepository
{
    private readonly NotificationDbContext _context;

    public NotificationPreferenceRepository(NotificationDbContext context)
    {
        _context = context;
    }

    public async Task<NotificationPreference?> GetByUserIdAsync(Guid userId)
    {
        return await _context.NotificationPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId);
    }

    public async Task<NotificationPreference> GetOrCreateByUserIdAsync(Guid userId)
    {
        var existing = await GetByUserIdAsync(userId);
        if (existing != null) return existing;

        var preference = new NotificationPreference
        {
            Id = Guid.NewGuid(),
            UserId = userId
        };
        await _context.NotificationPreferences.AddAsync(preference);
        await _context.SaveChangesAsync();
        return preference;
    }

    public async Task UpdateAsync(NotificationPreference preference)
    {
        _context.NotificationPreferences.Update(preference);
        await _context.SaveChangesAsync();
    }
}
