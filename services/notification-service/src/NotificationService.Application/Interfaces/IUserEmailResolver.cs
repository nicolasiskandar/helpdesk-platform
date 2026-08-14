namespace NotificationService.Application.Interfaces;

/// <summary>
/// Resolves a user's email address from the Identity Service. Implementations
/// must not throw — resolution failures should degrade to a fallback address.
/// </summary>
public interface IUserEmailResolver
{
    Task<string?> GetEmailAsync(Guid userId);
}
