using IdentityService.Application.DTOs;

namespace IdentityService.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, string ipAddress);
    Task<AuthResponse> LoginAsync(LoginRequest request, string ipAddress);
    Task<AuthResponse> RefreshAsync(RefreshRequest request, string ipAddress);
    Task LogoutAsync(LogoutRequest request, string ipAddress);
    Task<UserResponse> GetCurrentUserAsync(Guid userId);
}
