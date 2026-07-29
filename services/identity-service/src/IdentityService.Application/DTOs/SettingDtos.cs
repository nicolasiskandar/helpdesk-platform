namespace IdentityService.Application.DTOs;

public record SettingResponse(
    string Key,
    string Value,
    string? Description
);

public record UpdateSettingItem(
    string Key,
    string Value
);

public record UpdateSettingsRequest(
    IReadOnlyList<UpdateSettingItem> Settings
);
