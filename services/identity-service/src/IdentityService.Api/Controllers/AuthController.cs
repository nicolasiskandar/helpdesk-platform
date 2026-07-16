using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using IdentityService.Application.DTOs;
using IdentityService.Application.Interfaces;
using IdentityService.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace IdentityService.Api.Controllers;

/// <summary>
/// Authentication and user management endpoints.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IJwtTokenService _jwtTokenService;

    public AuthController(IAuthService authService, IJwtTokenService jwtTokenService)
    {
        _authService = authService;
        _jwtTokenService = jwtTokenService;
    }

    /// <summary>
    /// Register a new user account.
    /// </summary>
    /// <remarks>
    /// Creates a new user with the Employee role. Returns JWT access and refresh tokens.
    /// Password must be at least 8 characters with uppercase, lowercase, digit, and special character.
    /// </remarks>
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var ipAddress = GetClientIpAddress();
        var result = await _authService.RegisterAsync(request, ipAddress);
        return Created(string.Empty, result);
    }

    /// <summary>
    /// Authenticate with email and password.
    /// </summary>
    /// <remarks>
    /// Returns JWT access and refresh tokens on success.
    /// Refresh tokens are single-use with rotation — each refresh returns a new token
    /// and revokes the old one.
    /// </remarks>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var ipAddress = GetClientIpAddress();
        var result = await _authService.LoginAsync(request, ipAddress);
        return Ok(result);
    }

    /// <summary>
    /// Exchange a refresh token for a new access token.
    /// </summary>
    /// <remarks>
    /// Implements single-use token rotation. The old refresh token is revoked
    /// and a new pair (access + refresh) is returned. Reusing a revoked token fails.
    /// </remarks>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
    {
        var ipAddress = GetClientIpAddress();
        var result = await _authService.RefreshAsync(request, ipAddress);
        return Ok(result);
    }

    /// <summary>
    /// Revoke a refresh token (logout).
    /// </summary>
    /// <remarks>
    /// Invalidates the provided refresh token. The client should discard
    /// both the access and refresh tokens after calling this endpoint.
    /// </remarks>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
    {
        var ipAddress = GetClientIpAddress();
        await _authService.LogoutAsync(request, ipAddress);
        return NoContent();
    }

    /// <summary>
    /// Get the current authenticated user's profile.
    /// </summary>
    /// <remarks>
    /// Requires a valid JWT access token in the Authorization header.
    /// </remarks>
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetCurrentUser()
    {
        var userId = GetUserIdFromClaims();
        if (userId == null)
            return Unauthorized();

        var result = await _authService.GetCurrentUserAsync(userId.Value);
        return Ok(result);
    }

    /// <summary>
    /// Get the public RSA key in JWKS format.
    /// </summary>
    /// <remarks>
    /// Returns the public key used to verify JWTs signed by this service.
    /// Other services should fetch this once at startup and cache it.
    /// Follows the OpenID Connect Discovery standard (RFC 8414).
    /// </remarks>
    [HttpGet(".well-known/jwks.json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetJwks()
    {
        var publicKeyPem = _jwtTokenService.GetPublicKeyPem();
        var rsa = RSA.Create();
        rsa.ImportFromPem(publicKeyPem);
        var parameters = rsa.ExportParameters(false);

        var jwks = new
        {
            keys = new[]
            {
                new
                {
                    kty = "RSA",
                    use = "sig",
                    kid = "identity-rsa-key",
                    alg = "RS256",
                    n = Base64UrlEncoder.Encode(parameters.Modulus!),
                    e = Base64UrlEncoder.Encode(parameters.Exponent!)
                }
            }
        };

        return Ok(jwks);
    }

    private string GetClientIpAddress()
    {
        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private Guid? GetUserIdFromClaims()
    {
        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                  ?? User.FindFirst("sub")?.Value;

        if (Guid.TryParse(sub, out var userId))
            return userId;

        return null;
    }
}
