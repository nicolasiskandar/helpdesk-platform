using System.Security.Claims;
using IdentityService.Application.DTOs;
using IdentityService.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IdentityService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IConfiguration _configuration;

    public UsersController(IUserService userService, IConfiguration configuration)
    {
        _userService = userService;
        _configuration = configuration;
    }

    /// <summary>
    /// Gets all users (paginated, filterable).
    /// </summary>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(UserListResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsers(
        [FromQuery] string? search = null,
        [FromQuery] int? roleId = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _userService.GetUsersAsync(search, roleId, isActive, page, pageSize);
        return Ok(result);
    }

    /// <summary>
    /// Gets a user by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserById(Guid id)
    {
        var user = await _userService.GetUserByIdAsync(id);
        return Ok(user);
    }

    /// <summary>
    /// Resolves email addresses for a batch of user IDs. Intended for trusted service-to-service
    /// calls (e.g. the Notification Service) and guarded by the shared NOTIFICATION_SERVICE_KEY.
    /// </summary>
    [HttpPost("emails")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(UserEmailsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetEmailsByIds([FromBody] UserEmailsRequest request)
    {
        var serviceKey = _configuration["NOTIFICATION_SERVICE_KEY"];
        var isTrustedCaller = !string.IsNullOrEmpty(serviceKey)
            && Request.Headers["X-Notification-Service-Key"].ToString() == serviceKey;

        if (!isTrustedCaller)
        {
            return Unauthorized();
        }

        var userIds = request?.UserIds?.Distinct().ToList() ?? new List<Guid>();
        if (userIds.Count == 0)
        {
            return BadRequest(new ErrorResponse("At least one user ID is required."));
        }
        if (userIds.Count > 100)
        {
            return BadRequest(new ErrorResponse("Too many user IDs. The maximum is 100."));
        }

        var result = await _userService.GetEmailsByIdsAsync(userIds);
        return Ok(result);
    }

    /// <summary>
    /// Creates a new user.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        var user = await _userService.CreateUserAsync(request);
        return CreatedAtAction(nameof(GetUserById), new { id = user.Id }, user);
    }

    /// <summary>
    /// Updates a user's profile, role, or active status.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserRequest request)
    {
        var user = await _userService.UpdateUserAsync(id, request);
        return Ok(user);
    }

    /// <summary>
    /// Deactivates a user (soft delete).
    /// </summary>
    [HttpPatch("{id:guid}/deactivate")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateUser(Guid id)
    {
        var currentUserId = GetUserIdFromClaims();
        if (currentUserId == id)
            return BadRequest(new ErrorResponse("You cannot deactivate your own account."));

        await _userService.DeactivateUserAsync(id);
        return NoContent();
    }

    /// <summary>
    /// Permanently deletes a user.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        var currentUserId = GetUserIdFromClaims();
        if (currentUserId == id)
            return BadRequest(new ErrorResponse("You cannot delete your own account."));

        await _userService.DeleteUserAsync(id);
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
