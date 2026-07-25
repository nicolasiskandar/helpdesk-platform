using IdentityService.Application.DTOs;
using IdentityService.Application.Interfaces;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;

namespace IdentityService.Infrastructure.Services;

public class UserService : IUserService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;

    public UserService(IUnitOfWork unitOfWork, IPasswordHasher passwordHasher)
    {
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
    }

    public async Task<UserListResponse> GetUsersAsync(string? search, int? roleId, bool? isActive, int page, int pageSize)
    {
        var users = await _unitOfWork.Users.GetAllAsync(search, roleId, isActive, page, pageSize);
        var totalCount = await _unitOfWork.Users.GetCountAsync(search, roleId, isActive);

        var responses = users.Select(MapToResponse).ToList();
        return new UserListResponse(responses, totalCount, page, pageSize);
    }

    public async Task<UserResponse> GetUserByIdAsync(Guid id)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(id)
            ?? throw new KeyNotFoundException("User not found.");

        return MapToResponse(user);
    }

    public async Task<UserResponse> CreateUserAsync(CreateUserRequest request)
    {
        if (await _unitOfWork.Users.EmailExistsAsync(request.Email))
            throw new InvalidOperationException("A user with this email already exists.");

        if (request.RoleId == 1 && await _unitOfWork.Users.HasAdminAsync())
            throw new InvalidOperationException("An admin user already exists. Only one admin is allowed.");

        var role = await _unitOfWork.Roles.GetByIdAsync(request.RoleId)
            ?? throw new InvalidOperationException("Invalid role.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            FullName = request.FullName,
            RoleId = request.RoleId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Users.AddAsync(user);
        await _unitOfWork.SaveChangesAsync();

        user.Role = role;
        return MapToResponse(user);
    }

    public async Task<UserResponse> UpdateUserAsync(Guid id, UpdateUserRequest request)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(id)
            ?? throw new KeyNotFoundException("User not found.");

        if (request.Email is not null && request.Email != user.Email)
        {
            if (await _unitOfWork.Users.EmailExistsAsync(request.Email))
                throw new InvalidOperationException("A user with this email already exists.");
            user.Email = request.Email;
        }

        if (request.FullName is not null)
            user.FullName = request.FullName;

        if (request.RoleId.HasValue && request.RoleId.Value != user.RoleId)
        {
            if (request.RoleId.Value == 1 && await _unitOfWork.Users.HasAdminAsync())
                throw new InvalidOperationException("An admin user already exists. Only one admin is allowed.");

            var role = await _unitOfWork.Roles.GetByIdAsync(request.RoleId.Value)
                ?? throw new InvalidOperationException("Invalid role.");
            user.RoleId = request.RoleId.Value;
            user.Role = role;
        }

        if (request.IsActive.HasValue)
            user.IsActive = request.IsActive.Value;

        await _unitOfWork.Users.UpdateAsync(user);
        await _unitOfWork.SaveChangesAsync();

        return MapToResponse(user);
    }

    public async Task DeactivateUserAsync(Guid id)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(id)
            ?? throw new KeyNotFoundException("User not found.");

        user.IsActive = false;
        await _unitOfWork.Users.UpdateAsync(user);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task DeleteUserAsync(Guid id)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(id)
            ?? throw new KeyNotFoundException("User not found.");

        await _unitOfWork.Users.DeleteAsync(user);
        await _unitOfWork.SaveChangesAsync();
    }

    private static UserResponse MapToResponse(User user)
    {
        return new UserResponse(
            user.Id,
            user.Email,
            user.FullName,
            user.Role.Name,
            user.IsActive,
            user.CreatedAt,
            user.LastLoginAt
        );
    }
}
