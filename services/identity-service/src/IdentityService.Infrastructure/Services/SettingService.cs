using IdentityService.Application.DTOs;
using IdentityService.Application.Interfaces;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;

namespace IdentityService.Infrastructure.Services;

public class SettingService : ISettingService
{
    private readonly IUnitOfWork _unitOfWork;

    private static readonly HashSet<string> KnownKeys =
    [
        "ticket_auto_close_days",
        "default_ticket_priority",
        "max_agent_active_tickets",
        "sla_high_hours",
        "sla_critical_hours",
        "allow_employee_ticket_create",
    ];

    public SettingService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<SettingResponse>> GetSettingsAsync()
    {
        var settings = await _unitOfWork.SystemSettings.GetAllAsync();
        return settings.Select(MapToResponse).ToList();
    }

    public async Task UpdateSettingsAsync(UpdateSettingsRequest request, Guid updatedByUserId)
    {
        var invalid = request.Settings.FirstOrDefault(s => !KnownKeys.Contains(s.Key));
        if (invalid is not null)
            throw new InvalidOperationException($"Unknown setting key: {invalid.Key}");

        var all = await _unitOfWork.SystemSettings.GetAllAsync();
        var now = DateTime.UtcNow;

        foreach (var item in request.Settings)
        {
            var setting = all.FirstOrDefault(s => s.Key == item.Key);
            if (setting is null)
                throw new InvalidOperationException($"Setting not found: {item.Key}");

            setting.Value = item.Value;
            setting.UpdatedAt = now;
            setting.UpdatedByUserId = updatedByUserId;
        }

        await _unitOfWork.SystemSettings.UpdateRangeAsync(all.Where(s => request.Settings.Any(r => r.Key == s.Key)));
        await _unitOfWork.SaveChangesAsync();
    }

    private static SettingResponse MapToResponse(SystemSetting setting)
    {
        return new SettingResponse(setting.Key, setting.Value, setting.Description);
    }
}
