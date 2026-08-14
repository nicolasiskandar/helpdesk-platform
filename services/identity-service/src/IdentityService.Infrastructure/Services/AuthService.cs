using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using IdentityService.Application.DTOs;
using IdentityService.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IdentityService.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AuthService> _logger;
    private readonly IConfiguration _configuration;
    private readonly int _refreshTokenExpiryDays;

    public AuthService(
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        IHttpClientFactory httpClientFactory,
        ILogger<AuthService> logger,
        IConfiguration configuration)
    {
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _configuration = configuration;

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

        var accessToken = _jwtTokenService.GenerateAccessToken(user.Id, user.Email, defaultRole.Name, user.FullName);
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

        var accessToken = _jwtTokenService.GenerateAccessToken(user.Id, user.Email, user.Role.Name, user.FullName);
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

        // Single-use rotation must be atomic: without it, two concurrent
        // refreshes carrying the same token would both pass the IsActive check
        // and mint two access/refresh pairs (token duplication).
        if (!await _unitOfWork.RefreshTokens.RevokeIfActiveAsync(hashedToken))
            throw new UnauthorizedAccessException("Refresh token is expired or has been revoked.");

        await LogActivityAsync(user.Id, "TokenRefresh", ipAddress);

        var accessToken = _jwtTokenService.GenerateAccessToken(user.Id, user.Email, user.Role.Name, user.FullName);
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

    public async Task<UserResponse> UpdateProfileAsync(Guid userId, UpdateProfileRequest request)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId)
            ?? throw new KeyNotFoundException("User not found.");

        if (!string.Equals(user.Email, request.Email, StringComparison.OrdinalIgnoreCase))
        {
            if (await _unitOfWork.Users.EmailExistsAsync(request.Email))
                throw new InvalidOperationException("A user with this email address already exists.");
            user.Email = request.Email;
        }

        user.FullName = request.FullName;
        await _unitOfWork.Users.UpdateAsync(user);
        await _unitOfWork.SaveChangesAsync();

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

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId)
            ?? throw new KeyNotFoundException("User not found.");

        if (!_passwordHasher.VerifyPassword(request.CurrentPassword, user.PasswordHash))
            throw new UnauthorizedAccessException("Current password is incorrect.");

        user.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);
        await _unitOfWork.Users.UpdateAsync(user);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        var user = await _unitOfWork.Users.GetByEmailAsync(request.Email);
        if (user == null)
        {
            // Don't reveal whether the email exists
            return;
        }

        var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(64));
        var tokenHash = ComputeSha256Hash(rawToken);

        var resetToken = new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.PasswordResetTokens.AddAsync(resetToken);
        await _unitOfWork.SaveChangesAsync();

        try
        {
            var frontendUrl = "http://localhost:3000";
            var resetLink = $"{frontendUrl}/reset-password?token={rawToken}";
            var htmlBody = $"""
            <p>Hello {user.FullName},</p>
            <p>A password reset was requested for your account.</p>
            <p><a href="{resetLink}">Reset your password</a></p>
            <p>This link expires in 15 minutes.</p>
            <p>If you did not request this, please ignore this email.</p>
            """;

            var emailRequest = new
            {
                toEmail = user.Email,
                subject = "Password Reset Request",
                htmlBody
            };

            var client = _httpClientFactory.CreateClient("NotificationService");
            var emailHttpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/email/send")
            {
                Content = JsonContent.Create(emailRequest)
            };
            emailHttpRequest.Headers.TryAddWithoutValidation(
                "X-Notification-Service-Key",
                _configuration["NOTIFICATION_SERVICE_KEY"]);
            var response = await client.SendAsync(emailHttpRequest);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send password reset email to {Email}", user.Email);
        }
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request)
    {
        var tokenHash = ComputeSha256Hash(request.Token);
        var storedToken = await _unitOfWork.PasswordResetTokens.GetByTokenHashAsync(tokenHash)
            ?? throw new InvalidOperationException("Invalid or expired reset token.");

        if (!storedToken.IsValid)
            throw new InvalidOperationException("Invalid or expired reset token.");

        var user = storedToken.User;
        user.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);
        await _unitOfWork.Users.UpdateAsync(user);
        await _unitOfWork.PasswordResetTokens.MarkAsUsedAsync(storedToken);
        await _unitOfWork.RefreshTokens.RevokeAllUserTokensAsync(user.Id);
        await _unitOfWork.SaveChangesAsync();
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
