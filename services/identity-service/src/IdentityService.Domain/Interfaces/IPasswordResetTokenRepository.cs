using IdentityService.Domain.Entities;

namespace IdentityService.Domain.Interfaces;

public interface IPasswordResetTokenRepository
{
    Task<PasswordResetToken?> GetByTokenHashAsync(string tokenHash);
    Task AddAsync(PasswordResetToken token);
    Task MarkAsUsedAsync(PasswordResetToken token);
}
