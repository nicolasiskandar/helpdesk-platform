namespace IdentityService.Domain.Interfaces;

public interface IUnitOfWork : IDisposable
{
    IUserRepository Users { get; }
    IRoleRepository Roles { get; }
    IRefreshTokenRepository RefreshTokens { get; }
    IActivityLogRepository ActivityLogs { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
