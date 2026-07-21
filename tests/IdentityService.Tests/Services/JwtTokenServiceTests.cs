using FluentAssertions;
using IdentityService.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace IdentityService.Tests.Services;

public class JwtTokenServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _privateKeyPath;
    private readonly string _publicKeyPath;
    private readonly JwtTokenService _sut;

    public JwtTokenServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"jwt-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _privateKeyPath = Path.Combine(_tempDir, "private.pem");
        _publicKeyPath = Path.Combine(_tempDir, "public.pem");

        // Generate RSA key pair to temp files
        var rsa = System.Security.Cryptography.RSA.Create(2048);
        File.WriteAllText(_privateKeyPath, rsa.ExportPkcs8PrivateKeyPem());
        File.WriteAllText(_publicKeyPath, rsa.ExportSubjectPublicKeyInfoPem());

        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["Jwt:PrivateKeyPath"]).Returns(_privateKeyPath);
        configMock.Setup(c => c["Jwt:PublicKeyPath"]).Returns(_publicKeyPath);
        configMock.Setup(c => c["Jwt:Issuer"]).Returns("test-issuer");
        configMock.Setup(c => c["Jwt:Audience"]).Returns("test-audience");
        configMock.Setup(c => c["Jwt:AccessTokenExpiryMinutes"]).Returns("15");
        configMock.Setup(c => c["Jwt:RefreshTokenExpiryDays"]).Returns("7");

        _sut = new JwtTokenService(configMock.Object);
    }

    [Fact]
    public void GetPublicKeyPem_ReturnsValidPemString()
    {
        // Act
        var pem = _sut.GetPublicKeyPem();

        // Assert
        pem.Should().NotBeNullOrWhiteSpace();
        pem.Should().StartWith("-----BEGIN PUBLIC KEY-----");
        pem.Should().EndWith("-----END PUBLIC KEY-----");
    }

    [Fact]
    public void GenerateAccessToken_ReturnsNonEmptyJwt()
    {
        // Act
        var token = _sut.GenerateAccessToken(Guid.NewGuid(), "test@example.com", "Employee");

        // Assert
        token.Should().NotBeNullOrWhiteSpace();
        token.Split('.').Should().HaveCount(3, "JWTs have 3 parts separated by dots");
    }

    [Fact]
    public void GenerateAccessToken_ContainsExpectedClaims()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var token = _sut.GenerateAccessToken(userId, "test@example.com", "Employee");

        // Assert - decode payload (base64url)
        var parts = token.Split('.');
        var payload = parts[1];
        // Add padding if needed
        payload = payload.Replace('-', '+').Replace('_', '/');
        switch (payload.Length % 4)
        {
            case 2: payload += "=="; break;
            case 3: payload += "="; break;
        }

        var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payload));
        json.Should().Contain(userId.ToString());
        json.Should().Contain("test@example.com");
    }

    [Fact]
    public void GenerateRefreshToken_ReturnsNonEmptyBase64()
    {
        // Act
        var token = _sut.GenerateRefreshToken();

        // Assert
        token.Should().NotBeNullOrWhiteSpace();
        // Should be valid base64
        Action act = () => Convert.FromBase64String(token);
        act.Should().NotThrow();
    }

    [Fact]
    public void GenerateRefreshToken_ReturnsUniqueValues()
    {
        // Act
        var token1 = _sut.GenerateRefreshToken();
        var token2 = _sut.GenerateRefreshToken();

        // Assert
        token1.Should().NotBe(token2);
    }

    [Fact]
    public void ValidateRefreshToken_ValidToken_ReturnsTrue()
    {
        // Arrange
        var token = _sut.GenerateRefreshToken();

        // Act
        var result = _sut.ValidateRefreshToken(token);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ValidateRefreshToken_NullToken_ReturnsFalse()
    {
        // Act
        var result = _sut.ValidateRefreshToken(null!);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ValidateRefreshToken_EmptyToken_ReturnsFalse()
    {
        // Act
        var result = _sut.ValidateRefreshToken("");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ValidateRefreshToken_WhitespaceToken_ReturnsFalse()
    {
        // Act
        var result = _sut.ValidateRefreshToken("   ");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ValidateRefreshToken_InvalidCharacters_ReturnsFalse()
    {
        // Act
        var result = _sut.ValidateRefreshToken("token with spaces!");

        // Assert
        result.Should().BeFalse();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }
}
