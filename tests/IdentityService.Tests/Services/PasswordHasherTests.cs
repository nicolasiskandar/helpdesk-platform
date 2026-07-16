using FluentAssertions;
using IdentityService.Infrastructure.Services;
using Xunit;

namespace IdentityService.Tests.Services;

public class PasswordHasherTests
{
    private readonly PasswordHasherService _sut = new();

    [Fact]
    public void HashPassword_ReturnsNonEmptyString()
    {
        var hash = _sut.HashPassword("Pass123!");

        hash.Should().NotBeNullOrWhiteSpace();
        hash.Should().NotBe("Pass123!");
    }

    [Fact]
    public void VerifyPassword_CorrectPassword_ReturnsTrue()
    {
        var hash = _sut.HashPassword("Pass123!");

        var result = _sut.VerifyPassword("Pass123!", hash);

        result.Should().BeTrue();
    }

    [Fact]
    public void VerifyPassword_WrongPassword_ReturnsFalse()
    {
        var hash = _sut.HashPassword("Pass123!");

        var result = _sut.VerifyPassword("WrongPass456!", hash);

        result.Should().BeFalse();
    }

    [Fact]
    public void HashPassword_SameInput_ProducesDifferentHashes()
    {
        var hash1 = _sut.HashPassword("Pass123!");
        var hash2 = _sut.HashPassword("Pass123!");

        hash1.Should().NotBe(hash2);
    }
}
