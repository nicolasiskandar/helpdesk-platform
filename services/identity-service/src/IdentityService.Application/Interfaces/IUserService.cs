using IdentityService.Application.DTOs;

namespace IdentityService.Application.Interfaces;

public interface IUserService
{
    Task<UserListResponse> GetUsersAsync(string? search, int? roleId, bool? isActive, int page, int pageSize);
    Task<UserResponse> GetUserByIdAsync(Guid id);
    Task<UserResponse> CreateUserAsync(CreateUserRequest request);
    Task<UserResponse> UpdateUserAsync(Guid id, UpdateUserRequest request);
    Task DeactivateUserAsync(Guid id);
    Task DeleteUserAsync(Guid id);
}
