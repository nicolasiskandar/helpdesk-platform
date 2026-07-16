namespace IdentityService.Domain.Interfaces;

public interface IJwtTokenService
{
    string GenerateAccessToken(Guid userId, string email, string role);
    string GenerateRefreshToken();
    bool ValidateRefreshToken(string token);
    string GetPublicKeyPem();
}
