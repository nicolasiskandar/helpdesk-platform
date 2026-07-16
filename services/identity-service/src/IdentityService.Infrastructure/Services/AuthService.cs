using System.Security.Cryptography;
using System.Text;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using IdentityService.Application.DTOs;
using IdentityService.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace IdentityService.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly int _refreshTokenExpiryDays;

    public AuthService(
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        IConfiguration configuration)
    {
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;

        if (!int.TryParse(configuration["Jwt:RefreshTokenExpiryDays"], out _refreshTokenExpiryDays))
            _refreshTokenExpiryDays = 7;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, string ipAddress)
    {
        if (await _unitOfWork.Users.EmailExistsAsync(request.Email))
            throw new InvalidOperationException("A user with this email address already exists.");

        var defaultRole = await _unitOfWork.Roles.GetByNameAsync("Employee")
            ?? throw new InvalidOperationException("Default role 'Employee' not found.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            FullName = request.FullName,
            RoleId = defaultRole.Id,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Users.AddAsync(user);
        await _unitOfWork.SaveChangesAsync();

        await LogActivityAsync(user.Id, "Register", ipAddress);
        await _unitOfWork.SaveChangesAsync();

        var accessToken = _jwtTokenService.GenerateAccessToken(user.Id, user.Email, defaultRole.Name);
        var refreshToken = await CreateRefreshTokenAsync(user.Id);
        await _unitOfWork.SaveChangesAsync();

        return new AuthResponse(
            AccessToken: accessToken,
            RefreshToken: refreshToken,
            ExpiresAt: DateTime.UtcNow.AddMinutes(15)
        );
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, string ipAddress)
    {
        var user = await _unitOfWork.Users.GetByEmailAsync(request.Email);

        if (user == null || !_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            if (user != null)
            {
                await LogActivityAsync(user.Id, "LoginFailed", ipAddress);
                await _unitOfWork.SaveChangesAsync();
            }
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        if (!user.IsActive)
            throw new UnauthorizedAccessException("This account has been deactivated.");

        user.LastLoginAt = DateTime.UtcNow;
        await _unitOfWork.Users.UpdateAsync(user);
        await LogActivityAsync(user.Id, "LoginSuccess", ipAddress);

        var accessToken = _jwtTokenService.GenerateAccessToken(user.Id, user.Email, user.Role.Name);
        var refreshToken = await CreateRefreshTokenAsync(user.Id);
        await _unitOfWork.SaveChangesAsync();

        return new AuthResponse(
            AccessToken: accessToken,
            RefreshToken: refreshToken,
            ExpiresAt: DateTime.UtcNow.AddMinutes(15)
        );
    }

    public async Task<AuthResponse> RefreshAsync(RefreshRequest request, string ipAddress)
    {
        var hashedToken = ComputeSha256Hash(request.RefreshToken);
        var storedToken = await _unitOfWork.RefreshTokens.GetByTokenAsync(hashedToken)
            ?? throw new UnauthorizedAccessException("Invalid refresh token.");

        if (!storedToken.IsActive)
            throw new UnauthorizedAccessException("Refresh token is expired or has been revoked.");

        var user = storedToken.User;
        if (!user.IsActive)
            throw new UnauthorizedAccessException("This account has been deactivated.");

        // Single-use rotation: revoke the old token
        await _unitOfWork.RefreshTokens.RevokeAsync(storedToken);
        await LogActivityAsync(user.Id, "TokenRefresh", ipAddress);

        var accessToken = _jwtTokenService.GenerateAccessToken(user.Id, user.Email, user.Role.Name);
        var newRefreshToken = await CreateRefreshTokenAsync(user.Id);

        await _unitOfWork.SaveChangesAsync();

        return new AuthResponse(
            AccessToken: accessToken,
            RefreshToken: newRefreshToken,
            ExpiresAt: DateTime.UtcNow.AddMinutes(15)
        );
    }

    public async Task LogoutAsync(LogoutRequest request, string ipAddress)
    {
        var hashedToken = ComputeSha256Hash(request.RefreshToken);
        var storedToken = await _unitOfWork.RefreshTokens.GetByTokenAsync(hashedToken)
            ?? throw new UnauthorizedAccessException("Invalid refresh token.");

        await _unitOfWork.RefreshTokens.RevokeAsync(storedToken);
        await LogActivityAsync(storedToken.UserId, "Logout", ipAddress);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<UserResponse> GetCurrentUserAsync(Guid userId)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId)
            ?? throw new KeyNotFoundException("User not found.");

        return new UserResponse(
            Id: user.Id,
            Email: user.Email,
            FullName: user.FullName,
            Role: user.Role.Name,
            IsActive: user.IsActive,
            CreatedAt: user.CreatedAt,
            LastLoginAt: user.LastLoginAt
        );
    }

    private async Task<string> CreateRefreshTokenAsync(Guid userId)
    {
        var tokenValue = _jwtTokenService.GenerateRefreshToken();
        var hashedToken = ComputeSha256Hash(tokenValue);

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = hashedToken,
            ExpiresAt = DateTime.UtcNow.AddDays(_refreshTokenExpiryDays),
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.RefreshTokens.AddAsync(refreshToken);
        return tokenValue;
    }

    private async Task LogActivityAsync(Guid userId, string action, string ipAddress)
    {
        await _unitOfWork.ActivityLogs.AddAsync(new UserActivityLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Action = action,
            IpAddress = ipAddress,
            Timestamp = DateTime.UtcNow
        });
    }

    private static string ComputeSha256Hash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(bytes);
    }
}
