using FluentAssertions;
using IdentityService.Application.DTOs;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using IdentityService.Infrastructure.Services;
using Moq;
using Xunit;

namespace IdentityService.Tests.Services;

public class UserServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IPasswordHasher> _passwordHasherMock = new();
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly Mock<IRoleRepository> _roleRepoMock = new();
    private readonly UserService _sut;

    public UserServiceTests()
    {
        _unitOfWorkMock.Setup(u => u.Users).Returns(_userRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.Roles).Returns(_roleRepoMock.Object);

        _sut = new UserService(_unitOfWorkMock.Object, _passwordHasherMock.Object);
    }

    // ---------- GetUsersAsync ----------

    [Fact]
    public async Task GetUsersAsync_ReturnsPaginatedResults()
    {
        // Arrange
        var users = new List<User>
        {
            new()
            {
                Id = Guid.NewGuid(), Email = "a@test.com", FullName = "Alice",
                RoleId = 1, IsActive = true, CreatedAt = DateTime.UtcNow,
                Role = new Role { Id = 1, Name = "Admin" }
            }
        };

        _userRepoMock.Setup(r => r.GetAllAsync(null, null, null, 1, 10)).ReturnsAsync(users);
        _userRepoMock.Setup(r => r.GetCountAsync(null, null, null)).ReturnsAsync(1);

        // Act
        var result = await _sut.GetUsersAsync(null, null, null, 1, 10);

        // Assert
        result.Users.Should().HaveCount(1);
        result.TotalCount.Should().Be(1);
        result.Page.Should().Be(1);
    }

    // ---------- CreateUserAsync ----------

    [Fact]
    public async Task CreateUserAsync_Success_CreatesUser()
    {
        // Arrange
        var role = new Role { Id = 3, Name = "Employee" };
        _userRepoMock.Setup(r => r.EmailExistsAsync("new@test.com")).ReturnsAsync(false);
        _roleRepoMock.Setup(r => r.GetByIdAsync(3)).ReturnsAsync(role);
        _passwordHasherMock.Setup(p => p.HashPassword("Pass123!")).Returns("hashed");

        var request = new CreateUserRequest("new@test.com", "Pass123!", "New User", 3);

        // Act
        var result = await _sut.CreateUserAsync(request);

        // Assert
        result.Email.Should().Be("new@test.com");
        result.FullName.Should().Be("New User");
        result.Role.Should().Be("Employee");
        result.IsActive.Should().BeTrue();

        _userRepoMock.Verify(r => r.AddAsync(It.Is<User>(u =>
            u.Email == "new@test.com" &&
            u.PasswordHash == "hashed" &&
            u.FullName == "New User" &&
            u.RoleId == 3)), Times.Once);

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateUserAsync_DuplicateEmail_ThrowsInvalidOperationException()
    {
        // Arrange
        _userRepoMock.Setup(r => r.EmailExistsAsync("existing@test.com")).ReturnsAsync(true);
        var request = new CreateUserRequest("existing@test.com", "Pass123!", "Test", 3);

        // Act
        var act = () => _sut.CreateUserAsync(request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task CreateUserAsync_InvalidRole_ThrowsInvalidOperationException()
    {
        // Arrange
        _userRepoMock.Setup(r => r.EmailExistsAsync("new@test.com")).ReturnsAsync(false);
        _roleRepoMock.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((Role?)null);
        var request = new CreateUserRequest("new@test.com", "Pass123!", "Test", 99);

        // Act
        var act = () => _sut.CreateUserAsync(request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Invalid role*");
    }

    [Fact]
    public async Task CreateUserAsync_AdminRoleAlreadyExists_ThrowsInvalidOperationException()
    {
        _userRepoMock.Setup(r => r.EmailExistsAsync("new@test.com")).ReturnsAsync(false);
        _userRepoMock.Setup(r => r.HasAdminAsync()).ReturnsAsync(true);

        var request = new CreateUserRequest("new@test.com", "Pass123!", "New Admin", 1);

        var act = () => _sut.CreateUserAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*admin*");
    }

    [Fact]
    public async Task CreateUserAsync_NonAdminRole_DoesNotCheckForAdmin()
    {
        var role = new Role { Id = 3, Name = "Employee" };
        _userRepoMock.Setup(r => r.EmailExistsAsync("new@test.com")).ReturnsAsync(false);
        _roleRepoMock.Setup(r => r.GetByIdAsync(3)).ReturnsAsync(role);
        _passwordHasherMock.Setup(p => p.HashPassword("Pass123!")).Returns("hashed");

        var request = new CreateUserRequest("new@test.com", "Pass123!", "New User", 3);

        var result = await _sut.CreateUserAsync(request);

        result.Role.Should().Be("Employee");
        _userRepoMock.Verify(r => r.HasAdminAsync(), Times.Never);
    }

    // ---------- UpdateUserAsync ----------

    [Fact]
    public async Task UpdateUserAsync_Success_UpdatesFields()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(), Email = "old@test.com", FullName = "Old Name",
            RoleId = 3, IsActive = true, CreatedAt = DateTime.UtcNow,
            Role = new Role { Id = 3, Name = "Employee" }
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);

        var request = new UpdateUserRequest(FullName: "New Name", Email: null, RoleId: null, IsActive: null);

        // Act
        var result = await _sut.UpdateUserAsync(user.Id, request);

        // Assert
        result.FullName.Should().Be("New Name");
        result.Email.Should().Be("old@test.com");
    }

    [Fact]
    public async Task UpdateUserAsync_ChangeEmail_DuplicateEmail_Throws()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(), Email = "old@test.com", FullName = "Test",
            RoleId = 3, IsActive = true, CreatedAt = DateTime.UtcNow,
            Role = new Role { Id = 3, Name = "Employee" }
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);
        _userRepoMock.Setup(r => r.EmailExistsAsync("taken@test.com")).ReturnsAsync(true);

        var request = new UpdateUserRequest(FullName: null, Email: "taken@test.com", RoleId: null, IsActive: null);

        // Act
        var act = () => _sut.UpdateUserAsync(user.Id, request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task UpdateUserAsync_UserNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        _userRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((User?)null);
        var request = new UpdateUserRequest(FullName: "Test", Email: null, RoleId: null, IsActive: null);

        // Act
        var act = () => _sut.UpdateUserAsync(Guid.NewGuid(), request);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ---------- DeactivateUserAsync ----------

    [Fact]
    public async Task DeactivateUserAsync_Success_SetsIsActiveFalse()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(), Email = "test@test.com", FullName = "Test",
            RoleId = 3, IsActive = true, CreatedAt = DateTime.UtcNow,
            Role = new Role { Id = 3, Name = "Employee" }
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);

        // Act
        await _sut.DeactivateUserAsync(user.Id);

        // Assert
        user.IsActive.Should().BeFalse();
        _userRepoMock.Verify(r => r.UpdateAsync(user), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeactivateUserAsync_UserNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        _userRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((User?)null);

        // Act
        var act = () => _sut.DeactivateUserAsync(Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ---------- DeleteUserAsync ----------

    [Fact]
    public async Task DeleteUserAsync_Success_DeletesUser()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(), Email = "test@test.com", FullName = "Test",
            RoleId = 3, IsActive = true, CreatedAt = DateTime.UtcNow,
            Role = new Role { Id = 3, Name = "Employee" }
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);

        // Act
        await _sut.DeleteUserAsync(user.Id);

        // Assert
        _userRepoMock.Verify(r => r.DeleteAsync(user), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------- GetUserByIdAsync ----------

    [Fact]
    public async Task GetUserByIdAsync_Success_ReturnsUser()
    {
        var user = new User
        {
            Id = Guid.NewGuid(), Email = "test@test.com", FullName = "Test User",
            RoleId = 3, IsActive = true, CreatedAt = new DateTime(2026, 1, 1),
            LastLoginAt = new DateTime(2026, 7, 15),
            Role = new Role { Id = 3, Name = "Employee" }
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);

        var result = await _sut.GetUserByIdAsync(user.Id);

        result.Id.Should().Be(user.Id);
        result.Email.Should().Be("test@test.com");
        result.FullName.Should().Be("Test User");
        result.Role.Should().Be("Employee");
        result.IsActive.Should().BeTrue();
        result.CreatedAt.Should().Be(new DateTime(2026, 1, 1));
        result.LastLoginAt.Should().Be(new DateTime(2026, 7, 15));
    }

    [Fact]
    public async Task GetUserByIdAsync_UserNotFound_ThrowsKeyNotFoundException()
    {
        _userRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((User?)null);

        var act = () => _sut.GetUserByIdAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*not found*");
    }

    // ---------- UpdateUserAsync additional cases ----------

    [Fact]
    public async Task UpdateUserAsync_ChangeEmail_Success()
    {
        var user = new User
        {
            Id = Guid.NewGuid(), Email = "old@test.com", FullName = "Test",
            RoleId = 3, IsActive = true, CreatedAt = DateTime.UtcNow,
            Role = new Role { Id = 3, Name = "Employee" }
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);
        _userRepoMock.Setup(r => r.EmailExistsAsync("new@test.com")).ReturnsAsync(false);

        var request = new UpdateUserRequest(FullName: null, Email: "new@test.com", RoleId: null, IsActive: null);

        var result = await _sut.UpdateUserAsync(user.Id, request);

        result.Email.Should().Be("new@test.com");
        user.Email.Should().Be("new@test.com");
    }

    [Fact]
    public async Task UpdateUserAsync_SameEmail_SkipsDuplicateCheck()
    {
        var user = new User
        {
            Id = Guid.NewGuid(), Email = "same@test.com", FullName = "Test",
            RoleId = 3, IsActive = true, CreatedAt = DateTime.UtcNow,
            Role = new Role { Id = 3, Name = "Employee" }
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);

        var request = new UpdateUserRequest(FullName: null, Email: "same@test.com", RoleId: null, IsActive: null);

        var result = await _sut.UpdateUserAsync(user.Id, request);

        result.Email.Should().Be("same@test.com");
        _userRepoMock.Verify(r => r.EmailExistsAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UpdateUserAsync_ChangeRoleId_Success()
    {
        var user = new User
        {
            Id = Guid.NewGuid(), Email = "test@test.com", FullName = "Test",
            RoleId = 3, IsActive = true, CreatedAt = DateTime.UtcNow,
            Role = new Role { Id = 3, Name = "Employee" }
        };
        var newRole = new Role { Id = 2, Name = "IT Support Agent" };

        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);
        _roleRepoMock.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(newRole);

        var request = new UpdateUserRequest(FullName: null, Email: null, RoleId: 2, IsActive: null);

        var result = await _sut.UpdateUserAsync(user.Id, request);

        result.Role.Should().Be("IT Support Agent");
        user.RoleId.Should().Be(2);
        user.Role.Should().Be(newRole);
    }

    [Fact]
    public async Task UpdateUserAsync_ChangeRoleId_InvalidRole_Throws()
    {
        var user = new User
        {
            Id = Guid.NewGuid(), Email = "test@test.com", FullName = "Test",
            RoleId = 3, IsActive = true, CreatedAt = DateTime.UtcNow,
            Role = new Role { Id = 3, Name = "Employee" }
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);
        _roleRepoMock.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((Role?)null);

        var request = new UpdateUserRequest(FullName: null, Email: null, RoleId: 99, IsActive: null);

        var act = () => _sut.UpdateUserAsync(user.Id, request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Invalid role*");
    }

    [Fact]
    public async Task UpdateUserAsync_SameRoleId_SkipsRoleLookup()
    {
        var user = new User
        {
            Id = Guid.NewGuid(), Email = "test@test.com", FullName = "Test",
            RoleId = 3, IsActive = true, CreatedAt = DateTime.UtcNow,
            Role = new Role { Id = 3, Name = "Employee" }
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);

        var request = new UpdateUserRequest(FullName: null, Email: null, RoleId: 3, IsActive: null);

        var result = await _sut.UpdateUserAsync(user.Id, request);

        result.Role.Should().Be("Employee");
        _roleRepoMock.Verify(r => r.GetByIdAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task UpdateUserAsync_SetIsActiveFalse_DeactivatesUser()
    {
        var user = new User
        {
            Id = Guid.NewGuid(), Email = "test@test.com", FullName = "Test",
            RoleId = 3, IsActive = true, CreatedAt = DateTime.UtcNow,
            Role = new Role { Id = 3, Name = "Employee" }
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);

        var request = new UpdateUserRequest(FullName: null, Email: null, RoleId: null, IsActive: false);

        var result = await _sut.UpdateUserAsync(user.Id, request);

        result.IsActive.Should().BeFalse();
        user.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateUserAsync_SetIsActiveTrue_ActivatesUser()
    {
        var user = new User
        {
            Id = Guid.NewGuid(), Email = "test@test.com", FullName = "Test",
            RoleId = 3, IsActive = false, CreatedAt = DateTime.UtcNow,
            Role = new Role { Id = 3, Name = "Employee" }
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);

        var request = new UpdateUserRequest(FullName: null, Email: null, RoleId: null, IsActive: true);

        var result = await _sut.UpdateUserAsync(user.Id, request);

        result.IsActive.Should().BeTrue();
        user.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateUserAsync_MultipleFields_UpdatesAll()
    {
        var user = new User
        {
            Id = Guid.NewGuid(), Email = "old@test.com", FullName = "Old Name",
            RoleId = 3, IsActive = true, CreatedAt = DateTime.UtcNow,
            Role = new Role { Id = 3, Name = "Employee" }
        };
        var newRole = new Role { Id = 1, Name = "Admin" };

        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);
        _userRepoMock.Setup(r => r.EmailExistsAsync("new@test.com")).ReturnsAsync(false);
        _roleRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(newRole);

        var request = new UpdateUserRequest(FullName: "New Name", Email: "new@test.com", RoleId: 1, IsActive: false);

        var result = await _sut.UpdateUserAsync(user.Id, request);

        result.FullName.Should().Be("New Name");
        result.Email.Should().Be("new@test.com");
        result.Role.Should().Be("Admin");
        result.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateUserAsync_ChangeToAdminRole_AlreadyExists_Throws()
    {
        var user = new User
        {
            Id = Guid.NewGuid(), Email = "test@test.com", FullName = "Test",
            RoleId = 3, IsActive = true, CreatedAt = DateTime.UtcNow,
            Role = new Role { Id = 3, Name = "Employee" }
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);
        _userRepoMock.Setup(r => r.HasAdminAsync()).ReturnsAsync(true);

        var request = new UpdateUserRequest(FullName: null, Email: null, RoleId: 1, IsActive: null);

        var act = () => _sut.UpdateUserAsync(user.Id, request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*admin*");
    }

    [Fact]
    public async Task UpdateUserAsync_KeepSameAdminRole_Succeeds()
    {
        var user = new User
        {
            Id = Guid.NewGuid(), Email = "admin@test.com", FullName = "Admin",
            RoleId = 1, IsActive = true, CreatedAt = DateTime.UtcNow,
            Role = new Role { Id = 1, Name = "Admin" }
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);
        _userRepoMock.Setup(r => r.HasAdminAsync()).ReturnsAsync(true);

        var request = new UpdateUserRequest(FullName: "Admin Updated", Email: null, RoleId: 1, IsActive: null);

        var result = await _sut.UpdateUserAsync(user.Id, request);

        result.FullName.Should().Be("Admin Updated");
        result.Role.Should().Be("Admin");
        _userRepoMock.Verify(r => r.HasAdminAsync(), Times.Never);
    }

    [Fact]
    public async Task UpdateUserAsync_ChangeFromAdminRole_Succeeds()
    {
        var user = new User
        {
            Id = Guid.NewGuid(), Email = "admin@test.com", FullName = "Admin",
            RoleId = 1, IsActive = true, CreatedAt = DateTime.UtcNow,
            Role = new Role { Id = 1, Name = "Admin" }
        };
        var newRole = new Role { Id = 3, Name = "Employee" };

        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);
        _roleRepoMock.Setup(r => r.GetByIdAsync(3)).ReturnsAsync(newRole);
        _userRepoMock.Setup(r => r.GetActiveAdminCountAsync()).ReturnsAsync(2);

        var request = new UpdateUserRequest(FullName: null, Email: null, RoleId: 3, IsActive: null);

        var result = await _sut.UpdateUserAsync(user.Id, request);

        result.Role.Should().Be("Employee");
        _userRepoMock.Verify(r => r.HasAdminAsync(), Times.Never);
    }

    [Fact]
    public async Task UpdateUserAsync_DemoteLastActiveAdmin_Throws()
    {
        var user = new User
        {
            Id = Guid.NewGuid(), Email = "admin@test.com", FullName = "Admin",
            RoleId = 1, IsActive = true, CreatedAt = DateTime.UtcNow,
            Role = new Role { Id = 1, Name = "Admin" }
        };
        var newRole = new Role { Id = 3, Name = "Employee" };

        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);
        _roleRepoMock.Setup(r => r.GetByIdAsync(3)).ReturnsAsync(newRole);
        _userRepoMock.Setup(r => r.GetActiveAdminCountAsync()).ReturnsAsync(1);

        var request = new UpdateUserRequest(FullName: null, Email: null, RoleId: 3, IsActive: null);

        var act = () => _sut.UpdateUserAsync(user.Id, request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*last active admin*");
    }

    [Fact]
    public async Task UpdateUserAsync_DeactivateLastActiveAdmin_Throws()
    {
        var user = new User
        {
            Id = Guid.NewGuid(), Email = "admin@test.com", FullName = "Admin",
            RoleId = 1, IsActive = true, CreatedAt = DateTime.UtcNow,
            Role = new Role { Id = 1, Name = "Admin" }
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);
        _userRepoMock.Setup(r => r.GetActiveAdminCountAsync()).ReturnsAsync(1);

        var request = new UpdateUserRequest(FullName: null, Email: null, RoleId: null, IsActive: false);

        var act = () => _sut.UpdateUserAsync(user.Id, request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*last active admin*");
    }

    // ---------- DeactivateUserAsync ----------

    [Fact]
    public async Task DeactivateUserAsync_LastActiveAdmin_Throws()
    {
        var user = new User
        {
            Id = Guid.NewGuid(), Email = "admin@test.com", FullName = "Admin",
            RoleId = 1, IsActive = true, CreatedAt = DateTime.UtcNow,
            Role = new Role { Id = 1, Name = "Admin" }
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);
        _userRepoMock.Setup(r => r.GetActiveAdminCountAsync()).ReturnsAsync(1);

        var act = () => _sut.DeactivateUserAsync(user.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*last active admin*");
        _userRepoMock.Verify(r => r.UpdateAsync(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task DeactivateUserAsync_NonLastAdmin_Succeeds()
    {
        var user = new User
        {
            Id = Guid.NewGuid(), Email = "admin@test.com", FullName = "Admin",
            RoleId = 1, IsActive = true, CreatedAt = DateTime.UtcNow,
            Role = new Role { Id = 1, Name = "Admin" }
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);
        _userRepoMock.Setup(r => r.GetActiveAdminCountAsync()).ReturnsAsync(2);

        await _sut.DeactivateUserAsync(user.Id);

        user.IsActive.Should().BeFalse();
        _userRepoMock.Verify(r => r.UpdateAsync(It.Is<User>(u => u.IsActive == false)), Times.Once);
    }

    [Fact]
    public async Task CreateUserAsync_AdminWhenOnlyAdminInactive_Succeeds()
    {
        var role = new Role { Id = 1, Name = "Admin" };
        _userRepoMock.Setup(r => r.EmailExistsAsync("newadmin@test.com")).ReturnsAsync(false);
        _userRepoMock.Setup(r => r.HasAdminAsync()).ReturnsAsync(false);
        _roleRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(role);
        _passwordHasherMock.Setup(p => p.HashPassword("Pass123!")).Returns("hashed");

        var request = new CreateUserRequest("newadmin@test.com", "Pass123!", "New Admin", 1);

        var result = await _sut.CreateUserAsync(request);

        result.Role.Should().Be("Admin");
        _userRepoMock.Verify(r => r.AddAsync(It.Is<User>(u => u.RoleId == 1)), Times.Once);
    }

    // ---------- GetUsersAsync filter cases ----------

    [Fact]
    public async Task GetUsersAsync_WithSearch_PassesFiltersToRepository()
    {
        var users = new List<User>
        {
            new() { Id = Guid.NewGuid(), Email = "alice@test.com", FullName = "Alice",
                RoleId = 3, IsActive = true, CreatedAt = DateTime.UtcNow,
                Role = new Role { Id = 3, Name = "Employee" } }
        };

        _userRepoMock.Setup(r => r.GetAllAsync("alice", null, null, 1, 10)).ReturnsAsync(users);
        _userRepoMock.Setup(r => r.GetCountAsync("alice", null, null)).ReturnsAsync(1);

        var result = await _sut.GetUsersAsync("alice", null, null, 1, 10);

        result.Users.Should().HaveCount(1);
        result.Users[0].FullName.Should().Be("Alice");
        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetUsersAsync_WithRoleId_PassesFiltersToRepository()
    {
        var users = new List<User>
        {
            new() { Id = Guid.NewGuid(), Email = "agent@test.com", FullName = "Agent",
                RoleId = 2, IsActive = true, CreatedAt = DateTime.UtcNow,
                Role = new Role { Id = 2, Name = "IT Support Agent" } }
        };

        _userRepoMock.Setup(r => r.GetAllAsync(null, 2, null, 1, 10)).ReturnsAsync(users);
        _userRepoMock.Setup(r => r.GetCountAsync(null, 2, null)).ReturnsAsync(1);

        var result = await _sut.GetUsersAsync(null, 2, null, 1, 10);

        result.Users.Should().HaveCount(1);
        result.Users[0].Role.Should().Be("IT Support Agent");
    }

    [Fact]
    public async Task GetUsersAsync_WithIsActive_PassesFiltersToRepository()
    {
        var users = new List<User>
        {
            new() { Id = Guid.NewGuid(), Email = "inactive@test.com", FullName = "Inactive",
                RoleId = 3, IsActive = false, CreatedAt = DateTime.UtcNow,
                Role = new Role { Id = 3, Name = "Employee" } }
        };

        _userRepoMock.Setup(r => r.GetAllAsync(null, null, false, 1, 10)).ReturnsAsync(users);
        _userRepoMock.Setup(r => r.GetCountAsync(null, null, false)).ReturnsAsync(1);

        var result = await _sut.GetUsersAsync(null, null, false, 1, 10);

        result.Users.Should().HaveCount(1);
        result.Users[0].IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task GetUsersAsync_CombinedFilters_PassesAllToRepository()
    {
        _userRepoMock.Setup(r => r.GetAllAsync("test", 3, true, 2, 5)).ReturnsAsync(new List<User>());
        _userRepoMock.Setup(r => r.GetCountAsync("test", 3, true)).ReturnsAsync(0);

        var result = await _sut.GetUsersAsync("test", 3, true, 2, 5);

        result.Users.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
        result.Page.Should().Be(2);
        result.PageSize.Should().Be(5);
    }

    [Fact]
    public async Task GetUsersAsync_EmptyResults_ReturnsEmptyList()
    {
        _userRepoMock.Setup(r => r.GetAllAsync(null, null, null, 1, 10)).ReturnsAsync(new List<User>());
        _userRepoMock.Setup(r => r.GetCountAsync(null, null, null)).ReturnsAsync(0);

        var result = await _sut.GetUsersAsync(null, null, null, 1, 10);

        result.Users.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    // ---------- GetEmailsByIdsAsync ----------

    [Fact]
    public async Task GetEmailsByIdsAsync_ReturnsMappedEmails()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        _userRepoMock.Setup(r => r.GetEmailsByIdsAsync(It.IsAny<IReadOnlyList<Guid>>()))
            .ReturnsAsync(new List<(Guid Id, string Email)>
            {
                (id1, "alice@test.com"),
                (id2, "bob@test.com")
            });

        var result = await _sut.GetEmailsByIdsAsync(new List<Guid> { id1, id2 });

        result.Users.Should().HaveCount(2);
        result.Users.Should().Contain(u => u.Id == id1 && u.Email == "alice@test.com");
        result.Users.Should().Contain(u => u.Id == id2 && u.Email == "bob@test.com");
    }

    [Fact]
    public async Task GetEmailsByIdsAsync_NoMatches_ReturnsEmptyList()
    {
        _userRepoMock.Setup(r => r.GetEmailsByIdsAsync(It.IsAny<IReadOnlyList<Guid>>()))
            .ReturnsAsync(new List<(Guid Id, string Email)>());

        var result = await _sut.GetEmailsByIdsAsync(new List<Guid> { Guid.NewGuid() });

        result.Users.Should().BeEmpty();
    }
}
