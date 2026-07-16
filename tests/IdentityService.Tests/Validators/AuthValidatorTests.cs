using FluentAssertions;
using FluentValidation.TestHelper;
using IdentityService.Application.DTOs;
using IdentityService.Application.Validators;
using Xunit;

namespace IdentityService.Tests.Validators;

public class AuthValidatorTests
{
    // ---------- RegisterRequestValidator ----------

    [Fact]
    public async Task RegisterRequest_ValidRequest_Passes()
    {
        var validator = new RegisterRequestValidator();
        var request = new RegisterRequest("test@example.com", "StrongPass1!", "John Doe");

        var result = await validator.TestValidateAsync(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("ab")]       // too short
    [InlineData("abcdefgh")] // no uppercase, no digit, no special
    [InlineData("Abcdefg1")] // no special char
    [InlineData("abcdefgh1!")] // no uppercase
    [InlineData("ABCDEFGH1!")] // no lowercase
    public async Task RegisterRequest_WeakPassword_Fails(string password)
    {
        var validator = new RegisterRequestValidator();
        var request = new RegisterRequest("test@example.com", password, "John Doe");

        var result = await validator.TestValidateAsync(request);

        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public async Task RegisterRequest_InvalidEmail_Fails(string email)
    {
        var validator = new RegisterRequestValidator();
        var request = new RegisterRequest(email, "StrongPass1!", "John Doe");

        var result = await validator.TestValidateAsync(request);

        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Theory]
    [InlineData("")]
    public async Task RegisterRequest_EmptyFullName_Fails(string fullName)
    {
        var validator = new RegisterRequestValidator();
        var request = new RegisterRequest("test@example.com", "StrongPass1!", fullName);

        var result = await validator.TestValidateAsync(request);

        result.ShouldHaveValidationErrorFor(x => x.FullName);
    }

    // ---------- LoginRequestValidator ----------

    [Fact]
    public async Task LoginRequest_ValidRequest_Passes()
    {
        var validator = new LoginRequestValidator();
        var request = new LoginRequest("test@example.com", "password");

        var result = await validator.TestValidateAsync(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("", "password")]
    public async Task LoginRequest_EmptyEmail_Fails(string email, string password)
    {
        var validator = new LoginRequestValidator();
        var request = new LoginRequest(email, password);

        var result = await validator.TestValidateAsync(request);

        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Theory]
    [InlineData("test@example.com", "")]
    public async Task LoginRequest_EmptyPassword_Fails(string email, string password)
    {
        var validator = new LoginRequestValidator();
        var request = new LoginRequest(email, password);

        var result = await validator.TestValidateAsync(request);

        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Theory]
    [InlineData("", "")]
    public async Task LoginRequest_BothEmpty_Fails(string email, string password)
    {
        var validator = new LoginRequestValidator();
        var request = new LoginRequest(email, password);

        var result = await validator.TestValidateAsync(request);

        result.ShouldHaveValidationErrorFor(x => x.Email);
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    // ---------- RefreshRequestValidator ----------

    [Fact]
    public async Task RefreshRequest_EmptyToken_Fails()
    {
        var validator = new RefreshRequestValidator();
        var request = new RefreshRequest("");

        var result = await validator.TestValidateAsync(request);

        result.ShouldHaveValidationErrorFor(x => x.RefreshToken);
    }

    // ---------- LogoutRequestValidator ----------

    [Fact]
    public async Task LogoutRequest_EmptyToken_Fails()
    {
        var validator = new LogoutRequestValidator();
        var request = new LogoutRequest("");

        var result = await validator.TestValidateAsync(request);

        result.ShouldHaveValidationErrorFor(x => x.RefreshToken);
    }
}
