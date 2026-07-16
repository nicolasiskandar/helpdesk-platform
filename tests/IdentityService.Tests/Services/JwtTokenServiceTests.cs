using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using FluentAssertions;
using IdentityService.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace IdentityService.Tests.Services;

public class JwtTokenServiceTests : IDisposable
{
    private readonly string _privateKeyPath;
    private readonly string _publicKeyPath;
    private readonly JwtTokenService _sut;

    public JwtTokenServiceTests()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"jwt_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        _privateKeyPath = Path.Combine(tempDir, "private.pem");
        _publicKeyPath = Path.Combine(tempDir, "public.pem");

        using var rsa = RSA.Create(2048);
        File.WriteAllText(_privateKeyPath, rsa.ExportPkcs8PrivateKeyPem());
        File.WriteAllText(_publicKeyPath, rsa.ExportSubjectPublicKeyInfoPem());

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:PrivateKeyPath"] = _privateKeyPath,
                ["Jwt:PublicKeyPath"] = _publicKeyPath,
                ["Jwt:Issuer"] = "test-issuer",
                ["Jwt:Audience"] = "test-audience",
                ["Jwt:AccessTokenExpiryMinutes"] = "15",
                ["Jwt:RefreshTokenExpiryDays"] = "7"
            })
            .Build();

        _sut = new JwtTokenService(config);
    }

    [Fact]
    public void GenerateAccessToken_ReturnsNonEmptyJwt()
    {
        var token = _sut.GenerateAccessToken(Guid.NewGuid(), "test@example.com", "Employee");

        token.Should().NotBeNullOrWhiteSpace();
        token.Split('.').Should().HaveCount(3);
    }

    [Fact]
    public void GenerateAccessToken_ContainsCorrectClaims()
    {
        var userId = Guid.NewGuid();
        var token = _sut.GenerateAccessToken(userId, "test@example.com", "Employee");

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        jwt.Claims.Should().Contain(c => c.Type == "sub" && c.Value == userId.ToString());
        jwt.Claims.Should().Contain(c => c.Type == "email" && c.Value == "test@example.com");
        jwt.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == "Employee");
        jwt.Issuer.Should().Be("test-issuer");
        jwt.Audiences.Should().Contain("test-audience");
    }

    [Fact]
    public void GenerateRefreshToken_ReturnsNonEmptyBase64String()
    {
        var token = _sut.GenerateRefreshToken();

        token.Should().NotBeNullOrWhiteSpace();
        Convert.FromBase64String(token).Should().NotBeEmpty();
    }

    [Fact]
    public void ValidateRefreshToken_ValidToken_ReturnsTrue()
    {
        var token = _sut.GenerateRefreshToken();

        var result = _sut.ValidateRefreshToken(token);

        result.Should().BeTrue();
    }

    [Fact]
    public void ValidateRefreshToken_EmptyToken_ReturnsFalse()
    {
        var result = _sut.ValidateRefreshToken("");

        result.Should().BeFalse();
    }

    public void Dispose()
    {
        var dir = Path.GetDirectoryName(_privateKeyPath);
        if (dir != null && Directory.Exists(dir))
            Directory.Delete(dir, true);
    }
}
