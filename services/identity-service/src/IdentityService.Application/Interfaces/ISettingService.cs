using IdentityService.Application.DTOs;

namespace IdentityService.Application.Interfaces;

public interface ISettingService
{
    Task<IReadOnlyList<SettingResponse>> GetSettingsAsync();
    Task UpdateSettingsAsync(UpdateSettingsRequest request, Guid updatedByUserId);
}
