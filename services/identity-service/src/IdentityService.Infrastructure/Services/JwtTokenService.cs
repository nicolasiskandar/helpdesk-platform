using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using IdentityService.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace IdentityService.Infrastructure.Services;

public class JwtTokenService : IJwtTokenService
{
    private readonly RSA _privateKeyRsa;
    private readonly RSA _publicKeyRsa;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _accessTokenExpiryMinutes;
    private readonly int _refreshTokenExpiryDays;

    public JwtTokenService(IConfiguration configuration)
    {
        var privateKeyPath = configuration["Jwt:PrivateKeyPath"]
            ?? throw new InvalidOperationException("Jwt:PrivateKeyPath is not configured.");
        var publicKeyPath = configuration["Jwt:PublicKeyPath"]
            ?? throw new InvalidOperationException("Jwt:PublicKeyPath is not configured.");

        _issuer = configuration["Jwt:Issuer"]
            ?? throw new InvalidOperationException("Jwt:Issuer is not configured.");
        _audience = configuration["Jwt:Audience"]
            ?? throw new InvalidOperationException("Jwt:Audience is not configured.");

        if (!int.TryParse(configuration["Jwt:AccessTokenExpiryMinutes"], out _accessTokenExpiryMinutes))
            _accessTokenExpiryMinutes = 15;
        if (!int.TryParse(configuration["Jwt:RefreshTokenExpiryDays"], out _refreshTokenExpiryDays))
            _refreshTokenExpiryDays = 7;

        _privateKeyRsa = RSA.Create();
        _privateKeyRsa.ImportFromPem(File.ReadAllText(privateKeyPath));

        _publicKeyRsa = RSA.Create();
        var publicKeyPem = File.ReadAllText(publicKeyPath);
        _publicKeyRsa.ImportFromPem(publicKeyPem);
    }

    public string GenerateAccessToken(Guid userId, string email, string role)
    {
        var securityKey = new RsaSecurityKey(_privateKeyRsa) { KeyId = "identity-rsa-key" };
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.RsaSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, role)
        };

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_accessTokenExpiryMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    public bool ValidateRefreshToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return false;
        return token.All(c => char.IsLetterOrDigit(c) || c == '+' || c == '/' || c == '=');
    }

    public string GetPublicKeyPem()
    {
        return _publicKeyRsa.ExportSubjectPublicKeyInfoPem();
    }
}
