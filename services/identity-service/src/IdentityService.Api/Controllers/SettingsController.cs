using System.Security.Claims;
using IdentityService.Application.DTOs;
using IdentityService.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IdentityService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class SettingsController : ControllerBase
{
    private readonly ISettingService _settingService;

    public SettingsController(ISettingService settingService)
    {
        _settingService = settingService;
    }

    /// <summary>
    /// Gets all system settings.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<SettingResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSettings()
    {
        var settings = await _settingService.GetSettingsAsync();
        return Ok(settings);
    }

    /// <summary>
    /// Updates system settings.
    /// </summary>
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdateSettingsRequest request)
    {
        var userId = GetUserIdFromClaims();
        await _settingService.UpdateSettingsAsync(request, userId);
        return NoContent();
    }

    private Guid GetUserIdFromClaims()
    {
        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                  ?? User.FindFirst("sub")?.Value;

        if (Guid.TryParse(sub, out var userId))
            return userId;

        return Guid.Empty;
    }
}
