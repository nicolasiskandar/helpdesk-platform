using IdentityService.Domain.Entities;

namespace IdentityService.Domain.Interfaces;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByTokenAsync(string token);
    Task<IReadOnlyList<RefreshToken>> GetActiveTokensByUserIdAsync(Guid userId);
    Task AddAsync(RefreshToken refreshToken);
    Task RevokeAsync(RefreshToken refreshToken);
    Task<bool> RevokeIfActiveAsync(string hashedToken);
    Task RevokeAllUserTokensAsync(Guid userId);
}
