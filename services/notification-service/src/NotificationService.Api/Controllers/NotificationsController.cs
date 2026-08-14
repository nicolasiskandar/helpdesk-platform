using System.Security.Claims;
using NotificationService.Application.DTOs;
using NotificationService.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NotificationService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(NotificationListResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetNotifications(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool? unreadOnly = null)
    {
        var userId = GetUserIdFromClaims();
        var notifications = await _notificationService.GetNotificationsAsync(userId, page, pageSize, unreadOnly);
        var totalCount = await _notificationService.GetCountAsync(userId, unreadOnly);
        return Ok(new NotificationListResponse(notifications, totalCount, page, pageSize));
    }

    [HttpGet("unread-count")]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUnreadCount()
    {
        var userId = GetUserIdFromClaims();
        var count = await _notificationService.GetUnreadCountAsync(userId);
        return Ok(count);
    }

    [HttpPatch("{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkAsRead(Guid id)
    {
        var userId = GetUserIdFromClaims();
        await _notificationService.MarkAsReadAsync(id, userId);
        return NoContent();
    }

    [HttpPatch("read-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var userId = GetUserIdFromClaims();
        await _notificationService.MarkAllAsReadAsync(userId);
        return NoContent();
    }

    [HttpGet("preferences")]
    [ProducesResponseType(typeof(PreferenceResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPreferences()
    {
        var userId = GetUserIdFromClaims();
        var prefs = await _notificationService.GetPreferencesAsync(userId);
        return Ok(prefs);
    }

    [HttpPut("preferences")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UpdatePreferences([FromBody] UpdatePreferenceRequest request)
    {
        var userId = GetUserIdFromClaims();
        await _notificationService.UpdatePreferencesAsync(userId, request);
        return NoContent();
    }

    private Guid GetUserIdFromClaims()
    {
        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                  ?? throw new UnauthorizedAccessException("User ID not found in token.");
        return Guid.Parse(sub);
    }
}
