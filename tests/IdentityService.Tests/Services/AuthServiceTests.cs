using FluentAssertions;
using IdentityService.Application.DTOs;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using IdentityService.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace IdentityService.Tests.Services;

public class AuthServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IPasswordHasher> _passwordHasherMock = new();
    private readonly Mock<IJwtTokenService> _jwtTokenServiceMock = new();
    private readonly Mock<IConfiguration> _configMock = new();
    private readonly Mock<IActivityLogRepository> _activityLogRepoMock = new();
    private readonly Mock<IRefreshTokenRepository> _refreshTokenRepoMock = new();
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _configMock.Setup(c => c["Jwt:RefreshTokenExpiryDays"]).Returns("7");
        _jwtTokenServiceMock.Setup(j => j.GenerateRefreshToken()).Returns("test-refresh-token");
        _unitOfWorkMock.Setup(u => u.ActivityLogs).Returns(_activityLogRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.RefreshTokens).Returns(_refreshTokenRepoMock.Object);

        _sut = new AuthService(
            _unitOfWorkMock.Object,
            _passwordHasherMock.Object,
            _jwtTokenServiceMock.Object,
            _configMock.Object);
    }

    // ---------- RegisterAsync ----------

    [Fact]
    public async Task RegisterAsync_Success_CreatesUserAndReturnsTokens()
    {
        // Arrange
        var role = new Role { Id = 3, Name = "Employee" };
        _unitOfWorkMock.Setup(u => u.Users.EmailExistsAsync("test@example.com")).ReturnsAsync(false);
        _unitOfWorkMock.Setup(u => u.Roles.GetByNameAsync("Employee")).ReturnsAsync(role);
        _passwordHasherMock.Setup(p => p.HashPassword("Pass123!")).Returns("hashed-password");
        _jwtTokenServiceMock.Setup(j => j.GenerateAccessToken(It.IsAny<Guid>(), "test@example.com", "Employee")).Returns("jwt-token");

        var request = new RegisterRequest("test@example.com", "Pass123!", "Test User");

        // Act
        var result = await _sut.RegisterAsync(request, "127.0.0.1");

        // Assert
        result.AccessToken.Should().Be("jwt-token");
        result.RefreshToken.Should().Be("test-refresh-token");
        result.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(15), TimeSpan.FromSeconds(5));

        _unitOfWorkMock.Verify(u => u.Users.AddAsync(It.Is<User>(u =>
            u.Email == "test@example.com" &&
            u.PasswordHash == "hashed-password" &&
            u.FullName == "Test User" &&
            u.RoleId == 3 &&
            u.IsActive == true)), Times.Once);

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.AtLeast(2));
    }

    [Fact]
    public async Task RegisterAsync_DuplicateEmail_ThrowsInvalidOperationException()
    {
        // Arrange
        _unitOfWorkMock.Setup(u => u.Users.EmailExistsAsync("existing@example.com")).ReturnsAsync(true);
        var request = new RegisterRequest("existing@example.com", "Pass123!", "Test User");

        // Act
        var act = () => _sut.RegisterAsync(request, "127.0.0.1");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");
    }

    // ---------- LoginAsync ----------

    [Fact]
    public async Task LoginAsync_Success_ReturnsTokensAndUpdatesLastLogin()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var role = new Role { Id = 3, Name = "Employee" };
        var user = new User
        {
            Id = userId,
            Email = "test@example.com",
            PasswordHash = "hashed-password",
            FullName = "Test User",
            RoleId = 3,
            Role = role,
            IsActive = true
        };

        _unitOfWorkMock.Setup(u => u.Users.GetByEmailAsync("test@example.com")).ReturnsAsync(user);
        _passwordHasherMock.Setup(p => p.VerifyPassword("Pass123!", "hashed-password")).Returns(true);
        _jwtTokenServiceMock.Setup(j => j.GenerateAccessToken(userId, "test@example.com", "Employee")).Returns("jwt-token");

        var request = new LoginRequest("test@example.com", "Pass123!");

        // Act
        var result = await _sut.LoginAsync(request, "127.0.0.1");

        // Assert
        result.AccessToken.Should().Be("jwt-token");
        result.RefreshToken.Should().Be("test-refresh-token");

        user.LastLoginAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        _unitOfWorkMock.Verify(u => u.Users.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            PasswordHash = "hashed-password",
            IsActive = true,
            Role = new Role { Name = "Employee" }
        };

        _unitOfWorkMock.Setup(u => u.Users.GetByEmailAsync("test@example.com")).ReturnsAsync(user);
        _passwordHasherMock.Setup(p => p.VerifyPassword("WrongPass123!", "hashed-password")).Returns(false);

        var request = new LoginRequest("test@example.com", "WrongPass123!");

        // Act
        var act = () => _sut.LoginAsync(request, "127.0.0.1");

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Invalid email or password*");
    }

    [Fact]
    public async Task LoginAsync_InactiveAccount_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            PasswordHash = "hashed-password",
            IsActive = false,
            Role = new Role { Name = "Employee" }
        };

        _unitOfWorkMock.Setup(u => u.Users.GetByEmailAsync("test@example.com")).ReturnsAsync(user);
        _passwordHasherMock.Setup(p => p.VerifyPassword("Pass123!", "hashed-password")).Returns(true);

        var request = new LoginRequest("test@example.com", "Pass123!");

        // Act
        var act = () => _sut.LoginAsync(request, "127.0.0.1");

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*deactivated*");
    }

    // ---------- RefreshAsync ----------

    [Fact]
    public async Task RefreshAsync_Success_RotatesToken()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = "test@example.com",
            IsActive = true,
            Role = new Role { Name = "Employee" }
        };
        var storedToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = "hashed-old-token",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            User = user
        };

        _unitOfWorkMock.Setup(u => u.RefreshTokens.GetByTokenAsync(It.IsAny<string>())).ReturnsAsync(storedToken);
        _jwtTokenServiceMock.Setup(j => j.GenerateAccessToken(userId, "test@example.com", "Employee")).Returns("new-jwt");

        var request = new RefreshRequest("old-refresh-token");

        // Act
        var result = await _sut.RefreshAsync(request, "127.0.0.1");

        // Assert
        result.AccessToken.Should().Be("new-jwt");
        result.RefreshToken.Should().Be("test-refresh-token");

        _unitOfWorkMock.Verify(u => u.RefreshTokens.RevokeAsync(storedToken), Times.Once);
    }

    [Fact]
    public async Task RefreshAsync_RevokedToken_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var storedToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Token = "hashed-token",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            RevokedAt = DateTime.UtcNow.AddHours(-1),
            User = new User { IsActive = true, Role = new Role { Name = "Employee" } }
        };

        _unitOfWorkMock.Setup(u => u.RefreshTokens.GetByTokenAsync(It.IsAny<string>())).ReturnsAsync(storedToken);

        var request = new RefreshRequest("revoked-token");

        // Act
        var act = () => _sut.RefreshAsync(request, "127.0.0.1");

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*expired or has been revoked*");
    }

    // ---------- LogoutAsync ----------

    [Fact]
    public async Task LogoutAsync_Success_RevokesToken()
    {
        // Arrange
        var storedToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Token = "hashed-token"
        };

        _unitOfWorkMock.Setup(u => u.RefreshTokens.GetByTokenAsync(It.IsAny<string>())).ReturnsAsync(storedToken);

        var request = new LogoutRequest("refresh-token");

        // Act
        await _sut.LogoutAsync(request, "127.0.0.1");

        // Assert
        _unitOfWorkMock.Verify(u => u.RefreshTokens.RevokeAsync(storedToken), Times.Once);
        _unitOfWorkMock.Verify(u => u.ActivityLogs.AddAsync(It.Is<UserActivityLog>(l =>
            l.Action == "Logout" && l.IpAddress == "127.0.0.1")), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------- GetCurrentUserAsync ----------

    [Fact]
    public async Task GetCurrentUserAsync_Success_ReturnsUserResponse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = "test@example.com",
            FullName = "Test User",
            IsActive = true,
            CreatedAt = new DateTime(2026, 1, 1),
            LastLoginAt = new DateTime(2026, 7, 15),
            Role = new Role { Name = "Employee" }
        };

        _unitOfWorkMock.Setup(u => u.Users.GetByIdAsync(userId)).ReturnsAsync(user);

        // Act
        var result = await _sut.GetCurrentUserAsync(userId);

        // Assert
        result.Id.Should().Be(userId);
        result.Email.Should().Be("test@example.com");
        result.FullName.Should().Be("Test User");
        result.Role.Should().Be("Employee");
        result.IsActive.Should().BeTrue();
        result.CreatedAt.Should().Be(new DateTime(2026, 1, 1));
        result.LastLoginAt.Should().Be(new DateTime(2026, 7, 15));
    }

    [Fact]
    public async Task GetCurrentUserAsync_UserNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        _unitOfWorkMock.Setup(u => u.Users.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((User?)null);

        // Act
        var act = () => _sut.GetCurrentUserAsync(Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*not found*");
    }

    // ---------- RegisterAsync additional cases ----------

    [Fact]
    public async Task RegisterAsync_DefaultRoleNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        _unitOfWorkMock.Setup(u => u.Users.EmailExistsAsync("test@example.com")).ReturnsAsync(false);
        _unitOfWorkMock.Setup(u => u.Roles.GetByNameAsync("Employee")).ReturnsAsync((Role?)null);

        var request = new RegisterRequest("test@example.com", "Pass123!", "Test User");

        // Act
        var act = () => _sut.RegisterAsync(request, "127.0.0.1");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    // ---------- LoginAsync additional cases ----------

    [Fact]
    public async Task LoginAsync_NonexistentEmail_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        _unitOfWorkMock.Setup(u => u.Users.GetByEmailAsync("nonexistent@example.com")).ReturnsAsync((User?)null);

        var request = new LoginRequest("nonexistent@example.com", "Pass123!");

        // Act
        var act = () => _sut.LoginAsync(request, "127.0.0.1");

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Invalid email or password*");
    }

    // ---------- RefreshAsync additional cases ----------

    [Fact]
    public async Task RefreshAsync_ExpiredToken_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var storedToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Token = "hashed-token",
            ExpiresAt = DateTime.UtcNow.AddDays(-1),
            User = new User { IsActive = true, Role = new Role { Name = "Employee" } }
        };

        _unitOfWorkMock.Setup(u => u.RefreshTokens.GetByTokenAsync(It.IsAny<string>())).ReturnsAsync(storedToken);

        var request = new RefreshRequest("expired-token");

        // Act
        var act = () => _sut.RefreshAsync(request, "127.0.0.1");

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*expired or has been revoked*");
    }

    [Fact]
    public async Task RefreshAsync_InvalidToken_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        _unitOfWorkMock.Setup(u => u.RefreshTokens.GetByTokenAsync(It.IsAny<string>())).ReturnsAsync((RefreshToken?)null);

        var request = new RefreshRequest("invalid-token");

        // Act
        var act = () => _sut.RefreshAsync(request, "127.0.0.1");

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Invalid refresh token*");
    }

    [Fact]
    public async Task RefreshAsync_DeactivatedUser_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var storedToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Token = "hashed-token",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            User = new User { IsActive = false, Role = new Role { Name = "Employee" } }
        };

        _unitOfWorkMock.Setup(u => u.RefreshTokens.GetByTokenAsync(It.IsAny<string>())).ReturnsAsync(storedToken);

        var request = new RefreshRequest("valid-token");

        // Act
        var act = () => _sut.RefreshAsync(request, "127.0.0.1");

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*deactivated*");
    }

    // ---------- LogoutAsync additional cases ----------

    [Fact]
    public async Task LogoutAsync_InvalidToken_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        _unitOfWorkMock.Setup(u => u.RefreshTokens.GetByTokenAsync(It.IsAny<string>())).ReturnsAsync((RefreshToken?)null);

        var request = new LogoutRequest("nonexistent-token");

        // Act
        var act = () => _sut.LogoutAsync(request, "127.0.0.1");

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Invalid refresh token*");
    }

    // ---------- UpdateProfileAsync ----------

    [Fact]
    public async Task UpdateProfileAsync_Success_UpdatesNameAndEmail()
    {
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = "old@example.com",
            FullName = "Old Name",
            IsActive = true,
            CreatedAt = new DateTime(2026, 1, 1),
            Role = new Role { Name = "Employee" }
        };

        _unitOfWorkMock.Setup(u => u.Users.GetByIdAsync(userId)).ReturnsAsync(user);
        _unitOfWorkMock.Setup(u => u.Users.EmailExistsAsync("new@example.com")).ReturnsAsync(false);

        var request = new UpdateProfileRequest("New Name", "new@example.com");

        var result = await _sut.UpdateProfileAsync(userId, request);

        result.FullName.Should().Be("New Name");
        result.Email.Should().Be("new@example.com");
        user.Email.Should().Be("new@example.com");
        user.FullName.Should().Be("New Name");
        _unitOfWorkMock.Verify(u => u.Users.UpdateAsync(user), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateProfileAsync_SameEmail_NoDuplicateCheck()
    {
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = "same@example.com",
            FullName = "Old Name",
            IsActive = true,
            CreatedAt = new DateTime(2026, 1, 1),
            Role = new Role { Name = "Employee" }
        };

        _unitOfWorkMock.Setup(u => u.Users.GetByIdAsync(userId)).ReturnsAsync(user);

        var request = new UpdateProfileRequest("New Name", "same@example.com");

        var result = await _sut.UpdateProfileAsync(userId, request);

        result.FullName.Should().Be("New Name");
        _unitOfWorkMock.Verify(u => u.Users.EmailExistsAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UpdateProfileAsync_DuplicateEmail_ThrowsInvalidOperationException()
    {
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = "old@example.com",
            FullName = "Old Name",
            IsActive = true,
            Role = new Role { Name = "Employee" }
        };

        _unitOfWorkMock.Setup(u => u.Users.GetByIdAsync(userId)).ReturnsAsync(user);
        _unitOfWorkMock.Setup(u => u.Users.EmailExistsAsync("taken@example.com")).ReturnsAsync(true);

        var request = new UpdateProfileRequest("New Name", "taken@example.com");

        var act = () => _sut.UpdateProfileAsync(userId, request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task UpdateProfileAsync_UserNotFound_ThrowsKeyNotFoundException()
    {
        _unitOfWorkMock.Setup(u => u.Users.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((User?)null);

        var request = new UpdateProfileRequest("Name", "email@test.com");

        var act = () => _sut.UpdateProfileAsync(Guid.NewGuid(), request);

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*not found*");
    }

    // ---------- ChangePasswordAsync ----------

    [Fact]
    public async Task ChangePasswordAsync_Success_HashesNewPassword()
    {
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = "test@example.com",
            PasswordHash = "old-hash",
            IsActive = true,
            Role = new Role { Name = "Employee" }
        };

        _unitOfWorkMock.Setup(u => u.Users.GetByIdAsync(userId)).ReturnsAsync(user);
        _passwordHasherMock.Setup(p => p.VerifyPassword("OldPass1!", "old-hash")).Returns(true);
        _passwordHasherMock.Setup(p => p.HashPassword("NewPass2!")).Returns("new-hash");

        var request = new ChangePasswordRequest("OldPass1!", "NewPass2!");

        await _sut.ChangePasswordAsync(userId, request);

        user.PasswordHash.Should().Be("new-hash");
        _unitOfWorkMock.Verify(u => u.Users.UpdateAsync(user), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ChangePasswordAsync_WrongCurrentPassword_ThrowsUnauthorizedAccessException()
    {
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = "test@example.com",
            PasswordHash = "old-hash",
            IsActive = true,
            Role = new Role { Name = "Employee" }
        };

        _unitOfWorkMock.Setup(u => u.Users.GetByIdAsync(userId)).ReturnsAsync(user);
        _passwordHasherMock.Setup(p => p.VerifyPassword("WrongPass!", "old-hash")).Returns(false);

        var request = new ChangePasswordRequest("WrongPass!", "NewPass2!");

        var act = () => _sut.ChangePasswordAsync(userId, request);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*incorrect*");
    }

    [Fact]
    public async Task ChangePasswordAsync_UserNotFound_ThrowsKeyNotFoundException()
    {
        _unitOfWorkMock.Setup(u => u.Users.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((User?)null);

        var request = new ChangePasswordRequest("OldPass1!", "NewPass2!");

        var act = () => _sut.ChangePasswordAsync(Guid.NewGuid(), request);

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*not found*");
    }
}
