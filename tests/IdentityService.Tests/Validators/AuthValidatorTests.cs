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

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task RegisterRequest_EmptyPassword_Fails(string? password)
    {
        var validator = new RegisterRequestValidator();
        var request = new RegisterRequest("test@example.com", password!, "John Doe");

        var result = await validator.TestValidateAsync(request);

        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public async Task RegisterRequest_PasswordExactly7Chars_Fails()
    {
        var validator = new RegisterRequestValidator();
        var request = new RegisterRequest("test@example.com", "Abc1!ef", "John Doe");

        var result = await validator.TestValidateAsync(request);

        result.ShouldHaveValidationErrorFor(x => x.Password);
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
    [InlineData("not-an-email", "password")]
    public async Task LoginRequest_InvalidEmail_Fails(string email, string password)
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

    [Fact]
    public async Task RefreshRequest_NullToken_Fails()
    {
        var validator = new RefreshRequestValidator();
        var request = new RefreshRequest(null!);

        var result = await validator.TestValidateAsync(request);

        result.ShouldHaveValidationErrorFor(x => x.RefreshToken);
    }

    [Fact]
    public async Task RefreshRequest_ValidRequest_Passes()
    {
        var validator = new RefreshRequestValidator();
        var request = new RefreshRequest("some-refresh-token");

        var result = await validator.TestValidateAsync(request);

        result.ShouldNotHaveAnyValidationErrors();
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

    [Fact]
    public async Task LogoutRequest_ValidRequest_Passes()
    {
        var validator = new LogoutRequestValidator();
        var request = new LogoutRequest("some-refresh-token");

        var result = await validator.TestValidateAsync(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    // ---------- RegisterRequest additional cases ----------

    [Fact]
    public async Task RegisterRequest_FullNameExceedsMaxLength_Fails()
    {
        var validator = new RegisterRequestValidator();
        var longName = new string('A', 201);
        var request = new RegisterRequest("test@example.com", "StrongPass1!", longName);

        var result = await validator.TestValidateAsync(request);

        result.ShouldHaveValidationErrorFor(x => x.FullName);
    }

    [Theory]
    [InlineData("a@test.com")]
    [InlineData("user.name@domain.co.uk")]
    public async Task RegisterRequest_ValidEmail_Passes(string email)
    {
        var validator = new RegisterRequestValidator();
        var request = new RegisterRequest(email, "StrongPass1!", "John Doe");

        var result = await validator.TestValidateAsync(request);

        result.ShouldNotHaveValidationErrorFor(x => x.Email);
    }
}
